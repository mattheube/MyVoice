namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    internal bool VerifySoundPlaying(SoundCard sound)=>sounds.State(sound.Item.Id).Playing;
}
