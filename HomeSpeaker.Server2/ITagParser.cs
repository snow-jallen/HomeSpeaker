using HomeSpeaker.Shared;
using Id3;

namespace HomeSpeaker.Server
{
    public interface ITagParser
    {
        Song CreateSong(string fullPath);
        void UpdateSongTags(string fullPath, string name, string artist, string album);
    }

    public class DefaultTagParser : ITagParser
    {
        private readonly ILogger<DefaultTagParser> logger;

        public DefaultTagParser(ILogger<DefaultTagParser> logger)
        {
            this.logger = logger;
        }

        public Song CreateSong(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);
            using var mp3 = new Mp3(fullPath);
            var tag = mp3.GetTag(Id3TagFamily.Version2X) ?? mp3.GetTag(Id3TagFamily.Version1X) ?? throw new ApplicationException("Unable to find MP3 tags for " + fullPath);
            var title = tag.Title?.Value?.Replace("\0", string.Empty) ?? string.Empty;
            if (title.Length == 0)
            {
                title = fileName.Replace(".mp3", string.Empty);
            }

            return new Song
            {
                Album = tag.Album.Value?.Replace("\0", string.Empty),
                Artist = tag.Artists.Value.FirstOrDefault()?.Replace("\0", string.Empty) ?? "[Artist Unknown]",
                Name = title,
                Path = fullPath
            };
        }

        public void UpdateSongTags(string fullPath, string name, string artist, string album)
        {
            try
            {
                logger.LogInformation("Updating MP3 tags for file: {fullPath}", fullPath);

                using var mp3 = new Mp3(fullPath, Mp3Permissions.ReadWrite);

                // Get or create a tag
                var tag = mp3.GetTag(Id3TagFamily.Version2X) ?? mp3.GetTag(Id3TagFamily.Version1X);

                if (tag != null)
                {
                    // Update the tag values
                    tag.Title.Value = name;
                    tag.Album.Value = album;
                    tag.Artists.Value.Clear();
                    tag.Artists.Value.Add(artist);

                    // Write the changes back to the file
                    mp3.WriteTag(tag, WriteConflictAction.Replace);

                    logger.LogInformation("Successfully updated MP3 tags for file: {fullPath}", fullPath);
                }
                else
                {
                    logger.LogWarning("No existing tags found and unable to create new tags for file: {fullPath}", fullPath);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error updating MP3 tags for file: {fullPath}", fullPath);
                throw;
            }
        }
    }
}
