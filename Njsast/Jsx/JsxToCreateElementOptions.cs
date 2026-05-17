namespace Njsast.Jsx;

public class JsxToCreateElementOptions
{
    public const string DefaultFactory = "React.createElement";

    public const string DefaultFragment = "React.Fragment";

    public string Factory { get; set; } = DefaultFactory;

    public string Fragment { get; set; } = DefaultFragment;
}
