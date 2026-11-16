using System.Collections.Concurrent;
using Tmds.DBus.Protocol;

namespace HomeSpeaker.Server2.Services.BleTemperature;

/// <summary>
/// Scans through BlueZ on the host's system D-Bus (in Docker: <c>/run/dbus</c> mounted, see
/// docker-compose.bluetooth.yml). Each burst opens its own connection and discovery session, so BlueZ ends the session
/// if the app dies, and other clients (Home Assistant) keep theirs. It never powers the adapter, changes its settings
/// or removes devices. <c>DuplicateData</c> makes BlueZ report every advertisement, not only changed ones, so a sensor
/// whose temperature holds steady still counts as heard.
/// </summary>
public sealed class BlueZScanner : IBleScanner
{
    private const string BlueZ = "org.bluez";
    private const string AdapterInterface = "org.bluez.Adapter1";
    private const string DeviceInterface = "org.bluez.Device1";
    private const string DevicePathMarker = "/dev_";

    /// <summary>The weakest BLE signal, used when BlueZ hasn't reported one, so an unknown never beats a real reading.</summary>
    private const int UnknownRssi = -127;

    private readonly ILogger<BlueZScanner> logger;

    public BlueZScanner(ILogger<BlueZScanner> logger) => this.logger = logger;

    public async Task ScanAsync(TimeSpan duration, Action<BleAdvertisement> onAdvertisement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(onAdvertisement);

        using var connection = new DBusConnection(DBusAddress.System ?? throw new InvalidOperationException("No system D-Bus address."));
        await connection.ConnectAsync();

        var objects = await getManagedObjectsAsync(connection);
        var adapter = objects.FirstOrDefault(o => o.IsAdapter)?.Path
            ?? throw new InvalidOperationException("BlueZ has no Bluetooth adapter.");

        // Names and signal strengths known so far; an advertisement's PropertiesChanged only carries what changed.
        var devices = new ConcurrentDictionary<string, (string? Name, int Rssi)>(StringComparer.Ordinal);
        foreach (var known in objects.Where(o => !o.IsAdapter))
        {
            devices[known.Path] = (known.Name, known.Rssi ?? UnknownRssi);
        }

        void handle(string path, Dictionary<string, VariantValue> properties)
        {
            // Filtered here rather than with a path_namespace match rule, which Tmds.DBus.Protocol 0.94.1 spells in a way
            // dbus-daemon rejects ("pathNamespace").
            if (!path.StartsWith(adapter + "/", StringComparison.Ordinal) || MacFromPath(path) is not { } mac)
            {
                return;
            }

            var (name, rssi) = devices.TryGetValue(path, out var seen) ? seen : (null, UnknownRssi);
            name = stringProperty(properties, "Name") ?? stringProperty(properties, "Alias") ?? name;
            if (properties.TryGetValue("RSSI", out var rssiValue) && rssiValue.Type == VariantValueType.Int16)
            {
                rssi = rssiValue.GetInt16();
            }

            devices[path] = (name, rssi);
            if (properties.TryGetValue("ManufacturerData", out var data) && manufacturerData(data) is { Count: > 0 } frames)
            {
                onAdvertisement(new BleAdvertisement(mac, name, rssi, frames));
            }
        }

        using var changed = await connection.AddMatchAsync(
            new MatchRule { Type = MessageType.Signal, Sender = BlueZ, Interface = "org.freedesktop.DBus.Properties", Member = "PropertiesChanged", Arg0 = DeviceInterface },
            static (Message message, object? _) =>
            {
                var reader = message.GetBodyReader();
                reader.ReadString();
                return (Path: message.PathAsString ?? "", Properties: reader.ReadDictionaryOfStringToVariantValue());
            },
            (Notification<(string Path, Dictionary<string, VariantValue> Properties)> notification) =>
            {
                if (notification.HasValue)
                {
                    handle(notification.Value.Path, notification.Value.Properties);
                }
            },
            emitOnCapturedContext: false,
            ObserverFlags.None);

        // A device BlueZ hadn't seen before arrives here, created from an advertisement it just received.
        using var added = await connection.AddMatchAsync(
            new MatchRule { Type = MessageType.Signal, Sender = BlueZ, Interface = "org.freedesktop.DBus.ObjectManager", Member = "InterfacesAdded" },
            static (Message message, object? _) =>
            {
                var reader = message.GetBodyReader();
                var path = reader.ReadObjectPathAsString();
                Dictionary<string, VariantValue>? device = null;
                var interfaces = reader.ReadDictionaryStart();
                while (reader.HasNext(interfaces))
                {
                    reader.AlignStruct();
                    var name = reader.ReadString();
                    var properties = reader.ReadDictionaryOfStringToVariantValue();
                    if (name == DeviceInterface)
                    {
                        device = properties;
                    }
                }

                return (Path: path, Properties: device);
            },
            (Notification<(string Path, Dictionary<string, VariantValue>? Properties)> notification) =>
            {
                if (notification.HasValue && notification.Value.Properties is { } properties)
                {
                    handle(notification.Value.Path, properties);
                }
            },
            emitOnCapturedContext: false,
            ObserverFlags.None);

        await callAsync(connection, adapter, "SetDiscoveryFilter", "a{sv}", static (ref MessageWriter writer) =>
        {
            var filter = writer.WriteDictionaryStart();
            writer.WriteDictionaryEntryStart();
            writer.WriteString("Transport");
            writer.WriteVariantString("le");
            writer.WriteDictionaryEntryStart();
            writer.WriteString("DuplicateData");
            writer.WriteVariantBool(true);
            writer.WriteDictionaryEnd(filter);
        });
        await callAsync(connection, adapter, "StartDiscovery");
        try
        {
            await Task.Delay(duration, cancellationToken);
        }
        finally
        {
            try
            {
                await callAsync(connection, adapter, "StopDiscovery");
            }
            catch (DBusErrorReplyException ex)
            {
                logger.LogDebug("StopDiscovery: {Error}", ex.Message);
            }
        }
    }

    /// <summary>The address in a BlueZ device path (/org/bluez/hci0/dev_C9_38_30_34_17_76), or null for other objects.</summary>
    public static string? MacFromPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var at = path.LastIndexOf(DevicePathMarker, StringComparison.Ordinal);
        if (at < 0)
        {
            return null;
        }

        var mac = path[(at + DevicePathMarker.Length)..];
        return mac.Length == 17 && !mac.Contains('/', StringComparison.Ordinal) ? mac.Replace('_', ':').ToUpperInvariant() : null;
    }

    private static string? stringProperty(Dictionary<string, VariantValue> properties, string key) =>
        properties.TryGetValue(key, out var value) && value.Type == VariantValueType.String ? value.GetString() : null;

    // ManufacturerData is a{qv}: company id -> variant holding the bytes.
    private static Dictionary<ushort, byte[]>? manufacturerData(VariantValue value)
    {
        if (value.Type != VariantValueType.Dictionary)
        {
            return null;
        }

        var result = new Dictionary<ushort, byte[]>();
        foreach (var (company, payload) in value.GetDictionary<ushort, VariantValue>())
        {
            if (bytes(payload) is { } data)
            {
                result[company] = data;
            }
        }

        return result;
    }

    private static byte[]? bytes(VariantValue value) => value.Type switch
    {
        VariantValueType.Variant => bytes(value.GetVariantValue()),
        VariantValueType.Array when value.ItemType == VariantValueType.Byte => value.GetArray<byte>(),
        _ => null,
    };

    // MessageWriter is a struct: the body has to be written through a ref, or it lands in a copy.
    private delegate void BodyWriter(ref MessageWriter writer);

    private sealed record BlueZObject(string Path, bool IsAdapter, string? Name, int? Rssi);

    private static Task<List<BlueZObject>> getManagedObjectsAsync(DBusConnection connection)
    {
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(BlueZ, "/", "org.freedesktop.DBus.ObjectManager", "GetManagedObjects", null, MessageFlags.None);
            message = writer.CreateMessage();
        }

        return connection.CallMethodAsync(message, static (Message reply, object? _) =>
        {
            var result = new List<BlueZObject>();
            var reader = reply.GetBodyReader();
            var objects = reader.ReadDictionaryStart();
            while (reader.HasNext(objects))
            {
                reader.AlignStruct();
                var path = reader.ReadObjectPathAsString();
                var interfaces = reader.ReadDictionaryStart();
                while (reader.HasNext(interfaces))
                {
                    reader.AlignStruct();
                    var name = reader.ReadString();
                    var properties = reader.ReadDictionaryOfStringToVariantValue();
                    if (name == AdapterInterface)
                    {
                        result.Add(new BlueZObject(path, true, null, null));
                    }
                    else if (name == DeviceInterface)
                    {
                        var rssi = properties.TryGetValue("RSSI", out var r) && r.Type == VariantValueType.Int16 ? r.GetInt16() : (int?)null;
                        result.Add(new BlueZObject(path, false, stringProperty(properties, "Name") ?? stringProperty(properties, "Alias"), rssi));
                    }
                }
            }

            return result;
        });
    }

    private static Task callAsync(DBusConnection connection, string adapter, string member, string? signature = null, BodyWriter? body = null)
    {
        MessageBuffer message;
        var writer = connection.GetMessageWriter();
        try
        {
            writer.WriteMethodCallHeader(BlueZ, adapter, AdapterInterface, member, signature, MessageFlags.None);
            body?.Invoke(ref writer);
            message = writer.CreateMessage();
        }
        finally
        {
            writer.Dispose();
        }

        return connection.CallMethodAsync(message);
    }
}
