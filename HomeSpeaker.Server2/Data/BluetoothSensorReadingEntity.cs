namespace HomeSpeaker.Server2.Data;

/// <summary>The last reading kept for one Bluetooth temperature sensor, so a restart isn't blank.</summary>
public class BluetoothSensorReadingEntity
{
    public string Mac { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double TemperatureC { get; set; }
    public double Humidity { get; set; }
    public int BatteryPercent { get; set; }
    public int Rssi { get; set; }
    public DateTime ObservedAt { get; set; }
    public string Reporter { get; set; } = string.Empty;
}
