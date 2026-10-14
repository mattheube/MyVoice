import json
import unittest
import numpy as np
from conversation import SpeechMask, timing_profile

class ConversationHealthTests(unittest.TestCase):
    def test_benchmark_protocol_for_fast_medium_and_slow_devices(self):
        for duration in [.1, .49, .55, .70, 1.2, 8.0]:
            with self.subTest(duration=duration):
                profile=json.loads(json.dumps(timing_profile(np.float64(duration),6,1)))
                self.assertIsInstance(profile['recommended'],bool)
                self.assertTrue(.64<=profile['hop']<=.96)
                self.assertEqual(profile['steps'],6)
        self.assertFalse(timing_profile(8,6,1)['recommended'])

    def test_silence_produces_no_voice_mask(self):
        mask,speaking=SpeechMask().mask(np.zeros(48000,np.float32))
        self.assertFalse(speaking)
        self.assertEqual(np.count_nonzero(mask),0)

if __name__=='__main__': unittest.main()
