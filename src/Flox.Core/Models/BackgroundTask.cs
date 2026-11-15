using System.Security.Claims;
using Flox.Core.Data;

namespace Flox.Core.Models;

/// <summary>A unit of work that runs in the background on the server, with its state, progress, inputs and outputs and the user it runs for.</summary>
public class BackgroundTask : GuidEntity
{
    /// <summary>When the task finished, whatever the outcome.</summary>
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

    /// <summary>A human-readable name of the task.</summary>
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

    /// <summary>The error text when the task failed.</summary>
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

    /// <summary>When the task is expected to finish.</summary>
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

    /// <summary>The named inputs of the task.</summary>
    public List<BackgroundTaskParameter> InputParameters
    {
        get;
    } = [];

    /// <summary>The last message the task reported.</summary>
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

    /// <summary>The named results of the task.</summary>
    public List<BackgroundTaskParameter> OutputParameters
    {
        get;
    } = [];

    /// <summary>How soon the task should be taken from the queue.</summary>
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

    /// <summary>How much of the task is done, from 0 to 100.</summary>
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

    /// <summary>When the task started running.</summary>
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

    /// <summary>Where the task is in its life cycle.</summary>
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

    /// <summary>A short text about the current step.</summary>
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

    /// <summary>How long the task may run before it is stopped.</summary>
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

    /// <summary>The W3C trace context of the caller, so the work is traced as part of the request that queued it.</summary>
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

    /// <summary>The kind of the task, one of <see cref="BackgroundTaskTypes"/>.</summary>
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

    /// <summary>The claims of the user the task runs for; not sent to clients.</summary>
    [ServerOnly]
    public List<BackgroundTaskClaim> UserClaims { get; } = [];

    /// <summary>Builds the user the task runs for from <see cref="UserClaims"/>.</summary>
    /// <returns>The user, or the default identity when no claims are stored.</returns>
    public ClaimsPrincipal GetUser()
    {
        if (UserClaims.Count == 0)
        {
            return Identities.DefaultIdentity;
        }

        var claims = UserClaims.Select(claim => new Claim(claim.Type, claim.Value));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, Constants.AUTHENTICATION_TYPE, Constants.NAME_CLAIM, ClaimTypes.Role));
    }

    /// <summary>Stores the claims of a user as the one the task runs for and makes the user the owner of the task.</summary>
    /// <param name="user">The user.</param>
    /// <exception cref="ArgumentException">The user has no id.</exception>
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
