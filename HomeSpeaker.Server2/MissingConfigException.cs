
namespace HomeSpeaker.Server2;

[Serializable]
internal class MissingConfigException : Exception
{
    public MissingConfigException()
    {
    }

    public MissingConfigException(string? message) : base(message)
    {
    }

    public MissingConfigException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

}