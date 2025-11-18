namespace Application.Shared;

public static class ErrorResults
{
    public const string UserNotFound = "User not found!";
    public const string EmailInUse = "Email already in use.";
    public const string PhoneInUse = "PhoneNumber already in use.";
    public const string SignInNotFound = "There is no actual secret requested for this user";
    public const string SignInFailed = "Too many incorrect tries, please sign in again.";
    public const string SignInCodeError = "Given Secret was incorrect. Remaining tries ({0})";
    public const string EntityNotFound = "No entity with given metadata could be found";
    public const string Forbidden = "Operation not allowed";
    public const string InvalidLegend = "Die gewählte Legende ist ungültig.";
    public const string InvalidChampion = "Der gewählte Champion passt nicht zur Legende.";
    public const string InvalidDeckCardSelection =
        "Mindestens eine Karte konnte nicht verwendet werden.";
    public const string InvalidDeckColors =
        "Mindestens eine Karte entspricht nicht den Farben der Legende.";
    public const string DeckAccessDenied = "Für dieses Deck besteht keine Berechtigung.";
    public const string DeckCommentNotFound = "Kommentar konnte nicht gefunden werden.";
}
