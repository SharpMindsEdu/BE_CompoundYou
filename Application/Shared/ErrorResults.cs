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
    public const string InvalidRuneSelection = "Mindestens eine Rune ist ungültig.";
    public const string InvalidBattlefieldSelection = "Mindestens ein Battlefield ist ungültig.";
    public const string InvalidSideboardSelection = "Mindestens eine Sideboard-Karte ist ungültig.";
    public const string InvalidMainDeckCount = "Main Deck muss exakt 39 Karten enthalten.";
    public const string InvalidSideboardCount = "Sideboard muss exakt 8 Karten enthalten.";
    public const string InvalidRuneDeckCount = "Rune Deck muss exakt 12 Karten enthalten.";
    public const string InvalidBattlefieldDeckCount =
        "Battlefield Deck muss exakt 3 unterschiedliche Battlefields enthalten.";
    public const string InvalidDeckCopyLimit =
        "Main Deck und Sideboard dürfen zusammen maximal 3 Kopien pro Karte enthalten.";
    public const string DeckAccessDenied = "Für dieses Deck besteht keine Berechtigung.";
    public const string DeckCommentNotFound = "Kommentar konnte nicht gefunden werden.";
    public const string SimulationDeckNotReady = "Deck ist nicht simulation-ready.";
    public const string SimulationNotFound = "Simulation konnte nicht gefunden werden.";
    public const string SimulationAlreadyCompleted = "Simulation wurde bereits abgeschlossen.";
    public const string SimulationActionNotFound = "Aktion ist nicht verfügbar.";
    public const string OptimizationRunNotFound =
        "Der Deck-Optimierungslauf konnte nicht gefunden werden.";
}
