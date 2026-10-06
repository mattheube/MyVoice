using System;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    public object[] AiModelChoices {get;}=[new { Id="fast",Name="Direct",Description="Latence minimale. Moins de contexte et d’expression ; adapté aux échanges rapides et au matériel moins puissant. Le CPU peut rester trop lent." },new { Id="conversation",Name="Conversation",Description="Flux continu avec davantage de contexte et le vocodeur V2. Conçu pour Discord et le jeu si le benchmark de votre voix confirme le débit. Conserve votre prosodie ; ne régénère pas le style AR." },new { Id="studio",Name="Expressive",Description="Transfert de style et qualité élevée, avec un délai important. Recommandé pour les enregistrements et les fichiers ; microphone par phrases." }];
    public object[] AiExpressionChoices {get;}=[new { Id="adaptive",Name="Adaptatif · énergie et intonation" },new { Id="reference",Name="Expression des extraits" },new { Id="source",Name="Conserver mon intonation" }];
    public string AiModel {get=>Settings.AiModel;set{if(AiBusy||value==Settings.AiModel)return;Settings.AiModel=value;ai.Model=value;Changed(nameof(AiModel));OnPropertyChanged(nameof(AiSupportsExpression));if(AiReady){StopAi();_=StartAiCommand.ExecuteAsync(null);}}}
    public string AiFileModel {get=>Settings.AiFileModel;set{Settings.AiFileModel=value;Changed(nameof(AiFileModel));OnPropertyChanged(nameof(AiSupportsExpression));}}
    public bool DeveloperMode {get=>Settings.DeveloperMode;set{Settings.DeveloperMode=value;Changed(nameof(DeveloperMode));}}
    public string AiExpression {get=>Settings.AiExpression;set{Settings.AiExpression=value;ai.Expression=value;Changed(nameof(AiExpression));}}
    [RelayCommand] private void FinishAiPhrase()=>finishAiPhrase=true;
    private volatile bool finishAiPhrase,aiSpeechSeen;
    private int aiQuietSamples;
    private bool PhraseMode=>AiModel=="studio"||(AiModel=="fast"&&ai.Device=="CPU");
}
