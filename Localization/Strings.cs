namespace InterviewPrepAPI.Localization;

public sealed class Strings
{
    public static class Auth
    {
        public const string RegisterSuccess = "Auth.Register.Success";
        public const string LoginSuccess = "Auth.Login.Success";
        public const string LoginInvalidCredentials = "Auth.Login.InvalidCredentials";
        public const string LogoutSuccess = "Auth.Logout.Success";
        public const string TokenRefreshed = "Auth.Token.Refreshed";
        public const string UserNotFound = "Auth.User.NotFound";
        public const string UserAlreadyRegistered = "Auth.User.AlreadyRegistered";
        public const string UsernameAlreadyRegistered = "Auth.User.UsernameAlreadyRegistered";
        public const string TokenRefreshNotFound = "Auth.Token.RefreshNotFound";
        public const string TokenInvalidRefresh = "Auth.Token.InvalidRefresh";
        public const string UserNotFoundGeneric = "Auth.User.NotFoundGeneric";
        public const string RegisterOtpSent = "Auth.Register.OtpSent";
        public const string EmailNotVerified = "Auth.Email.NotVerified";
        public const string VerifyEmailSuccess = "Auth.VerifyEmail.Success";
    }

    public static class Password
    {
        public const string ForgotSent = "Password.Forgot.Sent";
        public const string VerifySuccess = "Password.Verify.Success";
        public const string VerifyInvalidOrExpired = "Password.Verify.InvalidOrExpired";
        public const string VerifyLocked = "Password.Verify.Locked";
        public const string ResetSuccess = "Password.Reset.Success";
        public const string ResetInvalidToken = "Password.Reset.InvalidToken";
    }

    public static class Error
    {
        public const string TooManyRequests = "Error.TooManyRequests";
        public const string ResourceNotFound = "Error.ResourceNotFound";
        public const string RequestTimedOut = "Error.RequestTimedOut";
        public const string RequestCancelled = "Error.RequestCancelled";
        public const string ServiceUnavailable = "Error.ServiceUnavailable";
        public const string DatabaseError = "Error.DatabaseError";
        public const string Unexpected = "Error.Unexpected";
        public const string EmailSendFailed = "Error.EmailSendFailed";
    }
}
