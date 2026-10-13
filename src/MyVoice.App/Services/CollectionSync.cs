using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
namespace MyVoice.App.Services;
public static class CollectionSync
{
    // Retain containers and selection when device discovery returns the same identifiers.
    public static void ByKey<T>(ObservableCollection<T> target,IEnumerable<T> values,Func<T,string> key)
    {
        var desired=values.ToArray();
        for(int i=0;i<desired.Length;i++)
        {
            int index=-1;for(int j=i;j<target.Count;j++)if(key(target[j])==key(desired[i])){index=j;break;}
            if(index<0)target.Insert(i,desired[i]);
            else {if(index!=i)target.Move(index,i);if(!EqualityComparer<T>.Default.Equals(target[i],desired[i]))target[i]=desired[i];}
        }
        while(target.Count>desired.Length)target.RemoveAt(target.Count-1);
    }
}
