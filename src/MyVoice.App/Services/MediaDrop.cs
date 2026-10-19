using System;
using System.IO;
using System.Windows;
using MyVoice.App.ViewModels;
namespace MyVoice.App.Services;
public static class MediaDrop {
 public static readonly DependencyProperty ImageTargetProperty=DependencyProperty.RegisterAttached("ImageTarget",typeof(string),typeof(MediaDrop),new PropertyMetadata(null,Changed));
 public static void SetImageTarget(DependencyObject o,string value)=>o.SetValue(ImageTargetProperty,value);
 public static string GetImageTarget(DependencyObject o)=>(string)o.GetValue(ImageTargetProperty);
 private static void Changed(DependencyObject o,DependencyPropertyChangedEventArgs e){if(o is not FrameworkElement target)return;target.AllowDrop=true;target.PreviewDragOver+=(_,args)=>{args.Effects=args.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;args.Handled=true;};target.Drop+=(_,args)=>{args.Handled=true;if(target.DataContext is MainViewModel vm&&args.Data.GetData(DataFormats.FileDrop) is string[] files){if(files.Length!=1){vm.ShowToast(vm.T["DropOneImage"]);return;}vm.DropImage(GetImageTarget(target),files[0]);}};}
}
