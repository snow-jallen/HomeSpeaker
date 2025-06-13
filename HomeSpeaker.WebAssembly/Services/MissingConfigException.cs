using System.Runtime.Serialization;

namespace HomeSpeaker.WebAssembly.Services
{
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

#pragma warning disable SYSLIB0051 // Type or member is obsolete
        protected MissingConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
#pragma warning restore SYSLIB0051 // Type or member is obsolete
    }
}