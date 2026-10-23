import unittest
from expressive_text import parse
from text_voice import text_chunks

class ExpressiveTextTests(unittest.TestCase):
    def test_controls_are_not_spoken(self):
        self.assertEqual(parse("Bonjour[pause][expressive]vous ![long-pause][neutral]Au revoir"),
            [("text",("Bonjour",.5)),("pause",.35),("text",("vous !",.8)),("pause",.8),("text",("Au revoir",.5))])
    def test_unknown_controls_fail_before_synthesis(self):
        for text in ["[whisper]Hello", "Hello [breath]", "[pause]", "[unfinished", "Hello]", "x"*1001]:
            with self.assertRaises(ValueError):parse(text)
    def test_sentence_chunking_preserves_words(self):
        text="Les émotions restent naturelles. "*30
        self.assertEqual(" ".join(text_chunks(text)).split(),text.split())
        self.assertTrue(all(len(chunk)<=180 for chunk in text_chunks(text)))
    def test_expression_default_and_unicode(self):
        self.assertEqual(parse("Ça va ?\n你好。",.2),[("text",("Ça va ?\n你好。",.2))])

if __name__=="__main__":unittest.main()
