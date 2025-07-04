namespace Frontend.Presentation;

public partial record LoginModel
{
    private readonly INavigator _navigator;
    private readonly Frontend.Services.Auth.AuthService _auth;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(INavigator navigator, Frontend.Services.Auth.AuthService auth, ILogger<LoginModel> logger)
    {
        _navigator = navigator;
        _auth = auth;
        _logger = logger;
    }

    public IState<string> DisplayName => State<string>.Value(this, () => string.Empty);
    public IState<string?> Email => State<string?>.Value(this, () => null);
    public IState<string?> PhoneNumber => State<string?>.Value(this, () => null);
    public IState<string?> Code => State<string?>.Value(this, () => null);

    public async Task Register()
    {
        var name = await DisplayName;
        var email = await Email;
        var phone = await PhoneNumber;

        _logger.LogInformation("Attempting register for {Name}", name);
        var token = await _auth.RegisterAsync(name, email, phone);
        if (token is not null)
        {
            _logger.LogInformation("Register successful");
            await _navigator.NavigateViewModelAsync<MainModel>(this);
        }
    }

    public async Task Login()
    {
        var code = await Code;
        var email = await Email;
        var phone = await PhoneNumber;

        if (code is null)
        {
            return;
        }

        _logger.LogInformation("Attempting login");
        var token = await _auth.LoginAsync(code, email, phone);
        if (token is not null)
        {
            _logger.LogInformation("Login successful");
            await _navigator.NavigateViewModelAsync<MainModel>(this);
        }
    }
}
