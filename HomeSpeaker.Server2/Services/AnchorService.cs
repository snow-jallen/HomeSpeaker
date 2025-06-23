using HomeSpeaker.Server2.Data;
using HomeSpeaker.Shared;
using Microsoft.EntityFrameworkCore;

namespace HomeSpeaker.Server2.Services;

public class AnchorService
{
    private readonly MusicContext dbContext;
    private readonly ILogger<AnchorService> logger;

    public AnchorService(MusicContext dbContext, ILogger<AnchorService> logger)
    {
        this.dbContext = dbContext;
        this.logger = logger;
    }

    // Anchor Definition Management
    public async Task<IEnumerable<AnchorDefinition>> GetActiveAnchorDefinitionsAsync()
    {
        var definitions = await dbContext.AnchorDefinitions
            .Where(ad => ad.IsActive)
            .OrderBy(ad => ad.Name)
            .ToListAsync();

        return definitions.Select(d => new AnchorDefinition(d.Id, d.Name, d.Description, d.IsActive));
    }

    public async Task<AnchorDefinition> CreateAnchorDefinitionAsync(CreateAnchorDefinitionRequest request)
    {
        logger.LogInformation("Creating anchor definition: {name}", request.Name);

        var entity = new AnchorDefinitionEntity
        {
            Name = request.Name,
            Description = request.Description,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.AnchorDefinitions.AddAsync(entity);
        await dbContext.SaveChangesAsync();

        return new AnchorDefinition(entity.Id, entity.Name, entity.Description, entity.IsActive);
    }

    public async Task<AnchorDefinition?> UpdateAnchorDefinitionAsync(int id, CreateAnchorDefinitionRequest request)
    {
        var entity = await dbContext.AnchorDefinitions.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Anchor definition {id} not found for update", id);
            return null;
        }

        entity.Name = request.Name;
        entity.Description = request.Description;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Updated anchor definition {id}: {name}", id, request.Name);
        return new AnchorDefinition(entity.Id, entity.Name, entity.Description, entity.IsActive);
    }

    public async Task<bool> DeactivateAnchorDefinitionAsync(int id)
    {
        var entity = await dbContext.AnchorDefinitions.FindAsync(id);
        if (entity == null)
        {
            logger.LogWarning("Anchor definition {id} not found for deactivation", id);
            return false;
        }

        entity.IsActive = false;
        entity.DeactivatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Deactivated anchor definition {id}: {name}", id, entity.Name);
        return true;
    }

    // User Anchor Management (Template Management)
    public async Task<IEnumerable<UserAnchor>> GetUserAnchorsAsync(string userId)
    {
        var userAnchors = await dbContext.UserAnchors
            .Include(ua => ua.AnchorDefinition)
            .Where(ua => ua.UserId == userId)
            .OrderBy(ua => ua.AnchorDefinition!.Name)
            .ToListAsync();

        return userAnchors.Select(ua => new UserAnchor(ua.Id, ua.UserId, ua.AnchorDefinitionId, ua.CreatedAt));
    }

    public async Task<UserAnchor> AssignAnchorToUserAsync(AssignAnchorToUserRequest request)
    {
        // Check if user already has this anchor
        var existing = await dbContext.UserAnchors
            .FirstOrDefaultAsync(ua => ua.UserId == request.UserId && ua.AnchorDefinitionId == request.AnchorDefinitionId);

        if (existing != null)
        {
            logger.LogInformation("User {userId} already has anchor {anchorId}", request.UserId, request.AnchorDefinitionId);
            return new UserAnchor(existing.Id, existing.UserId, existing.AnchorDefinitionId, existing.CreatedAt);
        }

        var entity = new UserAnchorEntity
        {
            UserId = request.UserId,
            AnchorDefinitionId = request.AnchorDefinitionId,
            CreatedAt = DateTime.UtcNow
        };

        await dbContext.UserAnchors.AddAsync(entity);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Assigned anchor {anchorId} to user {userId}", request.AnchorDefinitionId, request.UserId);
        return new UserAnchor(entity.Id, entity.UserId, entity.AnchorDefinitionId, entity.CreatedAt);
    }

    public async Task<bool> RemoveAnchorFromUserAsync(string userId, int anchorDefinitionId)
    {
        var entity = await dbContext.UserAnchors
            .FirstOrDefaultAsync(ua => ua.UserId == userId && ua.AnchorDefinitionId == anchorDefinitionId);

        if (entity == null)
        {
            logger.LogWarning("User anchor not found for user {userId} and anchor {anchorId}", userId, anchorDefinitionId);
            return false;
        }

        dbContext.UserAnchors.Remove(entity);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Removed anchor {anchorId} from user {userId}", anchorDefinitionId, userId);
        return true;
    }

    // Daily Anchor Management (Temporal Records)
    public async Task CreateDailyAnchorsForUserAsync(string userId, DateOnly date)
    {
        // Check if daily anchors already exist for this user and date
        var existingCount = await dbContext.DailyAnchors
            .CountAsync(da => da.UserId == userId && da.Date == date);

        if (existingCount > 0)
        {
            logger.LogInformation("Daily anchors already exist for user {userId} on {date}", userId, date);
            return;
        }

        // Get user's current anchors
        var userAnchors = await dbContext.UserAnchors
            .Include(ua => ua.AnchorDefinition)
            .Where(ua => ua.UserId == userId && ua.AnchorDefinition!.IsActive)
            .ToListAsync();

        if (!userAnchors.Any())
        {
            logger.LogInformation("No active anchors found for user {userId}", userId);
            return;
        }

        // Create daily snapshots
        var dailyAnchors = userAnchors.Select(ua => new DailyAnchorEntity
        {
            UserId = userId,
            AnchorDefinitionId = ua.AnchorDefinitionId,
            Date = date,
            IsCompleted = false,
            AnchorName = ua.AnchorDefinition!.Name,
            AnchorDescription = ua.AnchorDefinition.Description,
            CreatedAt = DateTime.UtcNow
        });

        await dbContext.DailyAnchors.AddRangeAsync(dailyAnchors);
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Created {count} daily anchors for user {userId} on {date}", userAnchors.Count, userId, date);
    }

    public async Task<IEnumerable<DailyAnchor>> GetDailyAnchorsAsync(string userId, DateOnly date)
    {
        var dailyAnchors = await dbContext.DailyAnchors
            .Where(da => da.UserId == userId && da.Date == date)
            .OrderBy(da => da.AnchorName)
            .ToListAsync();

        return dailyAnchors.Select(da => new DailyAnchor(
            da.Id, da.UserId, da.AnchorDefinitionId, da.Date, da.IsCompleted, 
            da.CompletedAt ?? DateTime.MinValue, da.AnchorName, da.AnchorDescription));
    }

    public async Task<IEnumerable<DailyAnchor>> GetDailyAnchorsRangeAsync(string userId, DateOnly startDate, DateOnly endDate)
    {
        var dailyAnchors = await dbContext.DailyAnchors
            .Where(da => da.UserId == userId && da.Date >= startDate && da.Date <= endDate)
            .OrderBy(da => da.Date)
            .ThenBy(da => da.AnchorName)
            .ToListAsync();

        return dailyAnchors.Select(da => new DailyAnchor(
            da.Id, da.UserId, da.AnchorDefinitionId, da.Date, da.IsCompleted, 
            da.CompletedAt ?? DateTime.MinValue, da.AnchorName, da.AnchorDescription));
    }

    public async Task<bool> UpdateAnchorCompletionAsync(UpdateAnchorCompletionRequest request)
    {
        var entity = await dbContext.DailyAnchors.FindAsync(request.DailyAnchorId);
        if (entity == null)
        {
            logger.LogWarning("Daily anchor {id} not found for completion update", request.DailyAnchorId);
            return false;
        }

        entity.IsCompleted = request.IsCompleted;
        entity.CompletedAt = request.IsCompleted ? DateTime.UtcNow : null;
        await dbContext.SaveChangesAsync();

        logger.LogInformation("Updated daily anchor {id} completion to {completed}", request.DailyAnchorId, request.IsCompleted);
        return true;
    }

    // Get all users with anchors
    public async Task<IEnumerable<string>> GetUsersWithAnchorsAsync()
    {
        var users = await dbContext.UserAnchors
            .Include(ua => ua.AnchorDefinition)
            .Where(ua => ua.AnchorDefinition!.IsActive)
            .Select(ua => ua.UserId)
            .Distinct()
            .OrderBy(u => u)
            .ToListAsync();

        return users;
    }    // Get daily anchors for all users in a date range
    public async Task<Dictionary<string, List<DailyAnchor>>> GetAllUsersDailyAnchorsAsync(DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var start = startDate ?? DateOnly.FromDateTime(DateTime.Today.AddDays(-30));
        var end = endDate ?? DateOnly.FromDateTime(DateTime.Today);

        var dailyAnchors = await dbContext.DailyAnchors
            .Where(da => da.Date >= start && da.Date <= end)
            .OrderBy(da => da.UserId)
            .ThenBy(da => da.Date)
            .ThenBy(da => da.AnchorName)
            .ToListAsync();

        var result = new Dictionary<string, List<DailyAnchor>>();
        
        foreach (var da in dailyAnchors)
        {
            var dailyAnchor = new DailyAnchor(
                da.Id,
                da.UserId,
                da.AnchorDefinitionId,
                da.Date,
                da.IsCompleted,
                da.CompletedAt ?? DateTime.MinValue,
                da.AnchorName,
                da.AnchorDescription
            );

            if (!result.ContainsKey(da.UserId))
            {
                result[da.UserId] = new List<DailyAnchor>();
            }
            
            result[da.UserId].Add(dailyAnchor);
        }

        return result;
    }

    // Ensure daily anchors exist for today for all users with anchors
    public async Task EnsureTodayAnchorsForAllUsersAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        
        // Get all users who have active anchors
        var usersWithAnchors = await dbContext.UserAnchors
            .Include(ua => ua.AnchorDefinition)
            .Where(ua => ua.AnchorDefinition!.IsActive)
            .Select(ua => ua.UserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in usersWithAnchors)
        {
            await CreateDailyAnchorsForUserAsync(userId, today);
        }

        logger.LogInformation("Ensured daily anchors exist for {count} users on {date}", usersWithAnchors.Count, today);
    }
}
