namespace ThemeLab;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // The preset layer must exist before the first window resolves its DynamicResource tokens.
        PresetEngine.Apply(PresetEngine.Build(Preset.Standard, textScale: 1.0));
        base.OnStartup(e);
    }
}
