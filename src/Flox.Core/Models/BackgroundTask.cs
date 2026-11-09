using System.Security.Claims;
using Flox.Core.Data;

namespace Flox.Core.Models;

public class BackgroundTask : GuidEntity
{
    [Index]
    public DateTimeOffset? CompletedAt
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(CompletedAt));
            }
        }
    }

    public string? DisplayName
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(DisplayName));
            }
        }
    }

    [Index]
    public string? Error
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Error));
            }
        }
    }

    [Index]
    public DateTimeOffset? ExpectedCompletionAt
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(ExpectedCompletionAt));
            }
        }
    }

    public List<BackgroundTaskParameter> InputParameters
    {
        get;
    } = [];

    [Index]
    public string? Message
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Message));
            }
        }
    }

    public List<BackgroundTaskParameter> OutputParameters
    {
        get;
    } = [];

    public BackgroundTaskPriority Priority
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Priority));
            }
        }
    } = BackgroundTaskPriority.Normal;

    public int ProgressPercentage
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(ProgressPercentage));
            }
        }
    } = 0;

    [Index]
    public DateTimeOffset? StartedAt
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(StartedAt));
            }
        }
    }

    [Index]
    public BackgroundTaskStatus Status
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Status));
            }
        }
    } = BackgroundTaskStatus.Pending;

    [Index]
    public string? StatusText
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(StatusText));
            }
        }
    }

    public TimeSpan Timeout
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Timeout));
            }
        }
    } = TimeSpan.FromSeconds(60);

    public string? TraceParent
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(TraceParent));
            }
        }
    }

    [Index]
    public string Type
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                NotifyPropertyChanged(nameof(Type));
            }
        }
    } = BackgroundTaskTypes.Unspecified;

    [ServerOnly]
    public List<BackgroundTaskClaim> UserClaims { get; } = [];

    public ClaimsPrincipal GetUser()
    {
        if (UserClaims.Count == 0)
        {
            return Identities.DefaultIdentity;
        }

        var claims = UserClaims.Select(claim => new Claim(claim.Type, claim.Value));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Constants.AUTHENTICATION_TYPE, Constants.NAME_CLAIM, ClaimTypes.Role));
    }

    public void SetUser(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userId = user.GetUserId();
        if (string.IsNullOrEmpty(userId))
        {
            throw new ArgumentException("Cannot assign a background task user. User ID is undefined.");
        }

        UserClaims.Clear();
        UserClaims.AddRange(user.Claims
                                .Select(claim => new BackgroundTaskClaim
                                {
                                    Type = claim.Type,
                                    Value = claim.Value
                                })
                                .ToList());
        OwnerId = userId;
    }
}
