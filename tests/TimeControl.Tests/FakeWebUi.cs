using Jint;

namespace TimeControl.Tests;

/// <summary>
/// The game's web UI as FakePage.js fakes it, in Jint: a root page holding the HUD pages in iframes. Scripts the
/// plugin builds run in it exactly as the plugin sends them, from the root page, and tests set the page up and read it
/// back in JavaScript expressions.
/// </summary>
internal sealed class FakeWebUi
{
    private readonly Engine _engine = new();

    /// <param name="setup">Statements that open the HUD pages, such as <c>var countdown = openCountdown();</c>.</param>
    public FakeWebUi(string setup)
    {
        _engine.Execute(Resource("TimeControl.Tests.FakePage.js"));
        _engine.Execute(setup);
    }

    /// <summary>Runs a script as ExecuteJavaScript would, and returns the page's reply.</summary>
    public string Send(string script) => _engine.Evaluate(script).AsString();

    public void Run(string statements) => _engine.Execute(statements);

    public string Text(string expression) => _engine.Evaluate($"String({expression})").AsString();

    public bool Is(string expression) => _engine.Evaluate($"!!({expression})").AsBoolean();

    public double Number(string expression) => _engine.Evaluate($"Number({expression})").AsNumber();

    private static string Resource(string name)
    {
        using var stream = typeof(FakeWebUi).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
