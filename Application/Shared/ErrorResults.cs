namespace Application.Common;

public static class ErrorResults
{
    public const string UserNotFound = "User not found!";
    public const string EmailInUse = "Email already in use.";
    public const string PhoneInUse = "PhoneNumber already in use.";
    public const string SignInNotFound = "There is no actual secret requested for this user";
    public const string SignInFailed = "Too many incorrect tries, please sign in again.";
    public const string SignInCodeError = "Given Secret was incorrect. Remaining tries ({0})";
    public const string EntityNotFound = "No entity with given metadata could be found";
}
