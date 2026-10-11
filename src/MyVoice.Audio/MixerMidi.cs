using NAudio.Midi;
using System.Collections.Concurrent;
namespace MyVoice.Audio;
public sealed record MidiControl(string Device,string Kind,int Channel,int Number,int Value);
/// <summary>Device callback only enqueues; the UI/control thread consumes commands.</summary>
public sealed class MixerMidi : IDisposable
{
    private MidiIn? input;
    private readonly ConcurrentQueue<MidiControl> queue=new();
    private string? name;
    private int queued;
    public static string[] Devices(){var result=new List<string>();for(int i=0;i<MidiIn.NumberOfDevices;i++)result.Add(MidiIn.DeviceInfo(i).ProductName);return result.ToArray();}
    public void Open(string? device)
    {
        Dispose();while(queue.TryDequeue(out _)){}Interlocked.Exchange(ref queued,0);
        if(string.IsNullOrWhiteSpace(device))return;int index=Array.IndexOf(Devices(),device);if(index<0)throw new InvalidOperationException("Contrôleur MIDI indisponible.");
        name=device;input=new MidiIn(index);input.MessageReceived+=Receive;input.Start();
    }
    private void Receive(object? sender,MidiInMessageEventArgs e)
    {
        MidiControl? command=e.MidiEvent switch{ControlChangeEvent cc=>new(name!,"CC",cc.Channel,(int)cc.Controller,cc.ControllerValue),NoteOnEvent note when note.Velocity>0=>new(name!,"Note",note.Channel,note.NoteNumber,note.Velocity),_=>null};
        if(command!=null)Enqueue(command);
    }
    internal void Enqueue(MidiControl command){if(Interlocked.Increment(ref queued)>256){Interlocked.Decrement(ref queued);return;}queue.Enqueue(command);}
    public bool Read(out MidiControl? command){if(queue.TryDequeue(out command)){Interlocked.Decrement(ref queued);return true;}return false;}
    public void Dispose(){var old=input;input=null;if(old!=null){old.MessageReceived-=Receive;old.Stop();old.Dispose();}}
}
