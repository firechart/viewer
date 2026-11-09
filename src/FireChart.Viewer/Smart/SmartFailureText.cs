
using FireChart.Viewer.Core.Smart;

namespace FireChart.Viewer.Smart;

/// <summary>
/// The texts of the failures of a SMART sign-in for the user.
/// </summary>
public static class SmartFailureText
{
    /// <summary>
    /// The text of a failure in the current UI culture.
    /// </summary>
    /// <param name="failure">The failure.</param>
    /// <returns>The text.</returns>
    /// <example>
    /// <code>
    /// catch (SmartException exception)
    /// {
    ///     message = exception.Failure.ToMessage();
    /// }
    /// </code>
    /// </example>
    public static string ToMessage(this SmartFailure failure)
    {
        return failure switch
        {
            SmartFailure.DiscoveryFailed => Strings.Message_SmartDiscoveryFailed,
            SmartFailure.PkceNotSupported => Strings.Message_SmartPkceNotSupported,
            SmartFailure.StateMismatch => Strings.Message_SmartStateMismatch,
            SmartFailure.AuthorizationDenied => Strings.Message_SmartAuthorizationDenied,
            SmartFailure.UnknownIss => Strings.Message_SmartUnknownIss,
            SmartFailure.ClientNotConfigured => Strings.Message_SmartClientNotConfigured,
            var _ => Strings.Message_SmartTokenExchangeFailed
        };
    }
}
