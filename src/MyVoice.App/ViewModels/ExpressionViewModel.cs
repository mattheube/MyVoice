using System;
using CommunityToolkit.Mvvm.Input;
namespace MyVoice.App.ViewModels;
public partial class MainViewModel
{
    private object[]? aiModelChoices;
    public object[] AiModelChoices => aiModelChoices ??= [new MyVoice.App.Services.LocalizedChoice(T,"fast","Direct","ModeDirectTip"),new MyVoice.App.Services.LocalizedChoice(T,"conversation","Conversation","ModeConversationTip"),new MyVoice.App.Services.LocalizedChoice(T,"studio","Expressive","ModeExpressiveTip")];
    private object[]? aiExpressionChoices;
    public object[] AiExpressionChoices => aiExpressionChoices ??= [new MyVoice.App.Services.LocalizedChoice(T,"adaptive","ExpressionAdaptive",""),new MyVoice.App.Services.LocalizedChoice(T,"reference","ExpressionReference",""),new MyVoice.App.Services.LocalizedChoice(T,"source","ExpressionSource","")];
    public string AiModel {get=>Settings.AiModel;set{if(value is not("fast" or "conversation" or "studio")||AiBusy||value==Settings.AiModel)return;Settings.AiModel=value;ai.Model=value;Changed(nameof(AiModel));OnPropertyChanged(nameof(AiSupportsExpression));if(AiReady){StopAi();_=StartAiCommand.ExecuteAsync(null);}}}
    public string AiFileModel {get=>Settings.AiFileModel;set{if(value is not("fast" or "conversation" or "studio"))return;Settings.AiFileModel=value;Changed(nameof(AiFileModel));OnPropertyChanged(nameof(AiSupportsExpression));}}
    public bool DeveloperMode {get=>Settings.DeveloperMode;set{Settings.DeveloperMode=value;Changed(nameof(DeveloperMode));}}
    public string AiExpression {get=>Settings.AiExpression;set{if(value is not("adaptive" or "reference" or "source"))return;Settings.AiExpression=value;ai.Expression=value;Changed(nameof(AiExpression));}}
    [RelayCommand] private void FinishAiPhrase()=>finishAiPhrase=true;
    private volatile bool finishAiPhrase,aiSpeechSeen;
    private int aiQuietSamples;
    private bool PhraseMode=>AiModel=="studio"||(AiModel=="fast"&&ai.Device=="CPU");
}
