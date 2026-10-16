using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace MyVoice.App.Services;
// Virtualize rows while preserving the existing card templates and commands.
public sealed class VirtualCardList : ListBox
{
    public static readonly DependencyProperty CardsProperty=DependencyProperty.Register(nameof(Cards),typeof(IEnumerable),typeof(VirtualCardList),new PropertyMetadata(null,Changed));
    public IEnumerable? Cards {get=>(IEnumerable?)GetValue(CardsProperty);set=>SetValue(CardsProperty,value);}
    public static readonly DependencyProperty CardTemplateProperty=DependencyProperty.Register(nameof(CardTemplate),typeof(DataTemplate),typeof(VirtualCardList));
    public DataTemplate CardTemplate {get=>(DataTemplate)GetValue(CardTemplateProperty);set=>SetValue(CardTemplateProperty,value);}
    public double CardWidth {get;set;}=184;
    public double CardHeight {get;set;}=182;
    private int columns;
    private INotifyCollectionChanged? observed;
    public VirtualCardList()
    {
        Background=Brushes.Transparent;BorderThickness=new Thickness(0);Padding=new Thickness(0);
        SetValue(ScrollViewer.CanContentScrollProperty,true);SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
        VirtualizingPanel.SetIsVirtualizing(this,true);VirtualizingPanel.SetVirtualizationMode(this,VirtualizationMode.Recycling);
        var panel=new FrameworkElementFactory(typeof(StackPanel));panel.SetValue(StackPanel.OrientationProperty,Orientation.Horizontal);
        var row=new FrameworkElementFactory(typeof(ItemsControl));row.SetBinding(ItemsControl.ItemsSourceProperty,new Binding());
        row.SetBinding(ItemsControl.ItemTemplateProperty,new Binding(nameof(CardTemplate)){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(VirtualCardList),1)});
        row.SetValue(ItemsControl.ItemsPanelProperty,new ItemsPanelTemplate(panel));ItemTemplate=new DataTemplate{VisualTree=row};
        var container=new Style(typeof(ListBoxItem));container.Setters.Add(new Setter(PaddingProperty,new Thickness(0)));container.Setters.Add(new Setter(FocusableProperty,false));container.Setters.Add(new Setter(HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));container.Setters.Add(new Setter(TemplateProperty,new ControlTemplate(typeof(ListBoxItem)){VisualTree=new FrameworkElementFactory(typeof(ContentPresenter))}));ItemContainerStyle=container;
        Loaded+=(_,_)=>{Observe();Rebuild();};Unloaded+=(_,_)=>Unobserve();
        SizeChanged+=(_,_)=>{if(Math.Max(1,(int)(ActualWidth/CardWidth))!=columns)Rebuild();};
    }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e){var list=(VirtualCardList)d;list.Unobserve();if(list.IsLoaded)list.Observe();list.Rebuild();}
    private void Observe(){if(observed!=null)return;observed=Cards as INotifyCollectionChanged;if(observed!=null)observed.CollectionChanged+=CollectionChanged;}
    private void Unobserve(){if(observed!=null)observed.CollectionChanged-=CollectionChanged;observed=null;}
    private void CollectionChanged(object? s,NotifyCollectionChangedEventArgs e)=>Rebuild();
    private void Rebuild()
    {
        columns=Math.Max(1,(int)(ActualWidth/CardWidth));var rows=new List<object[]>();var row=new List<object>();
        if(Cards!=null)foreach(var item in Cards){row.Add(item);if(row.Count==columns){rows.Add(row.ToArray());row.Clear();}}if(row.Count>0)rows.Add(row.ToArray());
        ItemsSource=rows;Height=Math.Min(rows.Count*CardHeight,Math.Max(220,(Window.GetWindow(this)?.ActualHeight??900)*.55));
    }
}
