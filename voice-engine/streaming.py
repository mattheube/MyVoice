# SPDX-License-Identifier: GPL-3.0-only
import numpy as np

class OverlapJoin:
    """Withhold 20 ms, blend two renderings of the same interval, fixed output length."""
    def __init__(self, overlap=960):
        self.n=overlap
        self.tail=np.zeros(overlap,dtype=np.float32)
    def push(self, rendered, count):
        need=count+self.n
        if len(rendered)<need: rendered=np.pad(rendered,(need-len(rendered),0))
        current=rendered[-need:]
        weight=np.linspace(0,1,self.n,dtype=np.float32)
        result=np.concatenate([self.tail*(1-weight)+current[:self.n]*weight,current[self.n:-self.n]])
        self.tail=current[-self.n:].copy()
        return result.astype(np.float32)

def shape_dynamics(converted, original, preserve=True):
    """Preserve pauses and relative input energy, with interpolated 20 ms gain envelopes."""
    n=min(len(converted),len(original))
    if n==0:return converted
    points=[];gains=[]
    for start in range(0,n,960):
        dry=original[start:start+960];wet=converted[start:start+960]
        rms=float(np.sqrt(np.mean(dry*dry)))
        target=0.0 if rms<.0002 else min(3.,max(.25,rms/(float(np.sqrt(np.mean(wet*wet)))+1e-8))) if preserve else 1.
        points.append(start+len(dry)/2);gains.append(target)
    envelope=np.interp(np.arange(n),points,gains)
    return (converted[:n]*envelope).astype(np.float32)
