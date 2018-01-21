using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TitlerApp
{
    public partial class MainWindow : Window
    {
        const string TimeSpanMask = "hh':'mm':'ss'.'f";

        class Paragraph
        {
            public TimeSpan Start;
            public TimeSpan End;
            public string Text;

            public override string ToString()
            {
                return Start.ToString(TimeSpanMask) + " - " + End.ToString(TimeSpanMask) + " \"" + Text + "\"";
            }

            public void MoveStart(TimeSpan newStart)
            {
                var durr = End - Start;
                Start = newStart;
                End = newStart + durr;
            }

            public bool IsValid()
            {
                if (string.IsNullOrWhiteSpace(Text))
                    return false;
                return Regex.IsMatch(Text, @"[A-Za-z0-9]");
            }
        }

        const int MaxParagraphSeconds = 4;
        const int MinParagraphSeconds = 1;
        const int ReplayMilliseconds = 400;

        private string sourceFileName;

        private bool isPlaying = false;
        private bool isRepeating = true;
        private int changeCount = 0;

        private List<Paragraph> paragraphs = new List<Paragraph>();
        private int paragraphIndex;

        private string titlerFileName;
        private string srtFileName;
        private List<Paragraph> srtParagraphs = new List<Paragraph>();
        private DateTime srtFileTime;

        public MainWindow()
        {
            InitializeComponent();
            mediaElement.LoadedBehavior = MediaState.Manual;
            UpdateControls();
            var dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += dispatcherTimer_Tick;
            dispatcherTimer.Interval = TimeSpan.FromMilliseconds(100);
            dispatcherTimer.Start();
            textBlockOriginalShadow.TextAlignment = TextAlignment.Center;
        }

        private TimeSpan RoundTimeSpan(TimeSpan ts)
        {
            return TimeSpan.Parse(ts.ToString(TimeSpanMask));
        }

        private string EncodeText(string text)
        {
            return text.Replace("\r", "{CR}").Replace("\n", "{LF}").Replace("\t", "{TAB}").Replace("|", "{PIPE}");
        }

        private string DecodeText(string text)
        {
            return text.Replace("{CR}", "\r").Replace("{LF}", "\n").Replace("{TAB}", "\t").Replace("{PIPE}", "|");
        }

        private void LoadSubtitles()
        {
            paragraphs = System.IO.File.ReadAllLines(titlerFileName)
                .Select(l => l.Split('|'))
                .Select(l => new Paragraph
                {
                    Start = TimeSpan.Parse(l[0]),
                    End = TimeSpan.Parse(l[1]),
                    Text = DecodeText(l[2]),
                })
                .ToList();
        }

        private void SaveSubtitles()
        {
            if (changeCount > 0)
            {
                System.IO.File.WriteAllLines(titlerFileName, paragraphs.Select(p => p.Start.ToString(TimeSpanMask) + "|" + p.End.ToString(TimeSpanMask) + "|" + EncodeText(p.Text)));
                changeCount = 0;
            }
        }

        private bool playerActivated = false;

        private void AtivatePlayer()
        {
            mediaElement.Play();
            mediaElement.Pause();
            mediaElement.Position = TimeSpan.FromSeconds(0);
            playerActivated = true;
        }

        private int selectingFile = 0;

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            if (sourceFileName == null)
            {
                if (selectingFile > 0)
                    return;
                string fn;
                if ((fn = SelectFile()) != null)
                    LoadFile(fn);
                else
                {
                    Close();
                    return;
                }
            }
            if (!playerActivated)
                AtivatePlayer();
            if (isPlaying)
            {
                var paragraph = paragraphs[paragraphIndex];
                if (mediaElement.Position > paragraph.End && isRepeating)
                    mediaElement.Position = paragraph.Start;
            }
            UpdateControls();
        }

        private string SelectFile()
        {
            selectingFile++;
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog();
                //dlg.InitialDirectory = ".";// @"C:\Users\ronaldo\Videos\SubTitles";
                dlg.DefaultExt = ".mvk";
                dlg.Filter = "Matroska Videos Files (*.mkv)|*.mkv|Portable Videos Files (*.m4v)|*.m4v|Common Videos Files (*.avi)|*.avi";
                Nullable<bool> result = dlg.ShowDialog();
                if (result.HasValue && result.Value)
                    return dlg.FileName;
                return null;
            }
            finally
            {
                selectingFile--;
            }
        }

        private void CheckSrtFile()
        {
            if (System.IO.File.Exists(srtFileName))
            {
                var nowFileTime = System.IO.File.GetLastWriteTime(srtFileName);
                if (nowFileTime != srtFileTime)
                {
                    srtParagraphs = LoadSrt(srtFileName);
                    srtFileTime = nowFileTime;
                }
            }
            else
            {
                srtFileTime = DateTime.MinValue;
                srtParagraphs.Clear();
            }
        }

        private void LoadFile(string fn)
        {
            sourceFileName = fn;
            mediaElement.Source = new Uri(sourceFileName);
            titlerFileName = System.IO.Path.ChangeExtension(sourceFileName, ".titler");
            if (System.IO.File.Exists(titlerFileName))
                LoadSubtitles();
            else
                paragraphs.Clear();
            srtFileName = System.IO.Path.ChangeExtension(sourceFileName, ".srt");
            CheckSrtFile();
            paragraphIndex = 0;
            AtLeastOne();
            listBoxAllParagraphs.Items.Clear();
            foreach (var p in paragraphs)
                listBoxAllParagraphs.Items.Add(p);
            changeCount = 0;
            isPlaying = false;
            isRepeating = true;
            listBoxAllParagraphs.SelectedIndex = 0;
            UpdateControls();
            this.Title = "Titler - " + System.IO.Path.GetFileNameWithoutExtension(sourceFileName);
        }

        private Paragraph AtLeastOne()
        {
            if (paragraphs.Count == 0)
            {
                var paragraph = new Paragraph
                {
                    Start = TimeSpan.FromSeconds(0),
                    End = RoundTimeSpan(TimeSpan.FromSeconds(MaxParagraphSeconds)),
                    Text = "...",
                };
                paragraphs.Add(paragraph);
                paragraphIndex = 0;
                return paragraph;
            }
            else
            {
                paragraphIndex = Math.Max(0, Math.Min(paragraphs.Count - 1, paragraphIndex));
                var paragraph = paragraphs[paragraphIndex];
                return paragraph;
            }
        }

        private void UpdateTextBox(TextBox textBox, string text)
        {
            if (textBox.Text != text)
                textBox.Text = text;
        }

        private void UpdateContent(ContentControl control, string text)
        {
            if ((control.Content ?? "").ToString() != (text ?? ""))
                control.Content = text;
        }

        private bool IsListBoxFocused()
        {
            var elem = Keyboard.FocusedElement;
            if (elem is ListBoxItem)
                return (elem as ListBoxItem).Content == listBoxAllParagraphs.SelectedItem;
            return false;
        }

        private void Log(string msg)
        {
            //System.IO.File.AppendAllLines(@"\temp\titler.log", new[] { DateTime.Now.ToString("HHmmss") + ": " + msg });
        }

        private void UpdateControls()
        {
            if (isPlaying && !isRepeating)
            {
                var t = mediaElement.Position;
                var latest = paragraphs.LastOrDefault(p => p.Start <= t);
                if (latest != null)
                    paragraphIndex = paragraphs.IndexOf(latest);
            }
            var paragraph = paragraphIndex < paragraphs.Count ? paragraphs[paragraphIndex] : null;
            if (!IsListBoxFocused())
            {
                if (listBoxAllParagraphs.SelectedItem != paragraph)
                    listBoxAllParagraphs.SelectedItem = paragraph;
                if (isPlaying && listBoxAllParagraphs.SelectedIndex != -1)
                    listBoxAllParagraphs.ScrollIntoView(listBoxAllParagraphs.Items[listBoxAllParagraphs.SelectedIndex]);
            }
            if (!textBoxStart.IsFocused)
                UpdateTextBox(textBoxStart, paragraph != null ? paragraph.Start.ToString(TimeSpanMask) : "--:--:--.-");
            if (!textBoxEnd.IsFocused)
                UpdateTextBox(textBoxEnd, paragraph != null ? paragraph.End.ToString(TimeSpanMask) : "--:--:--.-");
            if (!textBoxContent.IsFocused)
                UpdateTextBox(textBoxContent, paragraph != null ? paragraph.Text : "---");
            UpdateContent(labelPositionTime, mediaElement.Position.ToString(TimeSpanMask));
            UpdateContent(labelParagraphIndex, "Speech " + (paragraphIndex + 1) + "/" + paragraphs.Count +
                " (" + (isPlaying ? isRepeating ? "Playing repeatedly..." : "Playing continously..." : "Stopped") + ")");
            UpdateContent(buttonPlay, isPlaying ? "p_Ause" : "pl_Ay");
            UpdateContent(buttonRepeat, isRepeating ? "con_Tinuous" : "repea_T");
            UpdateContent(buttonNext, paragraphIndex < paragraphs.Count - 1 ? "_Next" : "add _New");
            UpdateContent(buttonSlower, mediaElement.SpeedRatio < 1 ? "faste_R" : "slowe_R");

            var rightTip = GetEquivalentSubtitleAtPosition() ?? "";
            var tipSet = (imageGlympseEye.ToolTip ?? "").ToString();
            if (tipSet != rightTip)
                imageGlympseEye.ToolTip = rightTip;

            if (AlphasOnly(textBoxContent.Text) == AlphasOnly(rightTip))
            {
                if (textBlockOriginalShadow.Text != rightTip)
                    textBlockOriginalShadow.Text = rightTip;
            }
            else
            {
                var correctMask = Regex.Replace(rightTip, @"\w", x => (x.Value == x.Value.ToUpper() ? "X" : "x"));
                if (textBlockOriginalShadow.Text != correctMask)
                    textBlockOriginalShadow.Text = correctMask;
            }
        }

        private string AlphasOnly(string text)
        {
            text = Regex.Replace(text, @"<\w>", "");
            text = Regex.Replace(text, @"</\w>", "");
            text = Regex.Replace(text, @"[^\w]", "");
            return text.ToUpper();
        }

        private void buttonMarkStart_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetStart(paragraphIndex, RoundTimeSpan(mediaElement.Position));
            FixTiming();
            changeCount++;
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void FixTiming()
        {
            Paragraph prevParagraph = null;
            for (var i = 0; i < paragraphs.Count; i++)
            {
                var thisParagraph = paragraphs[i];
                var durr = thisParagraph.End - thisParagraph.Start;
                if (durr.TotalSeconds < MinParagraphSeconds)
                    durr = TimeSpan.FromSeconds(MinParagraphSeconds);
                if (prevParagraph != null && thisParagraph.Start < prevParagraph.End)
                    thisParagraph.Start = prevParagraph.End;
                thisParagraph.End = thisParagraph.Start + durr;
                prevParagraph = thisParagraph;
            }
        }

        private void buttonMarkEnd_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetEnd(paragraphIndex, RoundTimeSpan(mediaElement.Position));
            FixTiming();
            changeCount++;
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void SetStart(int index, TimeSpan newStart)
        {
            var thisParagraph = paragraphs[index];
            var oldEnd = thisParagraph.End;
            thisParagraph.MoveStart(newStart);
            var newEnd = thisParagraph.End;
            index++;
            while (index < paragraphs.Count && paragraphs[index].Start == oldEnd)
            {
                var p = paragraphs[index];
                var sh = newEnd - oldEnd;
                oldEnd = p.End;
                p.MoveStart(p.Start + sh);
                newEnd = p.End;
                index++;
            }
        }

        private void SetEnd(int index, TimeSpan newEnd)
        {
            var thisParagraph = paragraphs[index];
            var oldEnd = thisParagraph.End;
            thisParagraph.End = newEnd;
            index++;
            while (index < paragraphs.Count && paragraphs[index].Start == oldEnd)
            {
                var p = paragraphs[index];
                var sh = newEnd - oldEnd;
                oldEnd = p.End;
                p.MoveStart(p.Start + sh);
                newEnd = p.End;
                index++;
            }
        }

        private void buttonStartPred_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetStart(paragraphIndex, RoundTimeSpan(paragraph.Start - TimeSpan.FromMilliseconds(ReplayMilliseconds)));
            FixTiming();
            changeCount++;
            mediaElement.Position = paragraph.Start;
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void buttonStartSucc_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetStart(paragraphIndex, RoundTimeSpan(paragraph.Start + TimeSpan.FromMilliseconds(ReplayMilliseconds)));
            FixTiming();
            changeCount++;
            mediaElement.Position = paragraph.Start;
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void buttonEndPred_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetEnd(paragraphIndex, RoundTimeSpan(paragraph.End - TimeSpan.FromMilliseconds(ReplayMilliseconds)));
            FixTiming();
            changeCount++;
            mediaElement.Position = paragraph.End - TimeSpan.FromMilliseconds(ReplayMilliseconds);
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void buttonEndSucc_Click(object sender, RoutedEventArgs e)
        {
            var paragraph = paragraphs[paragraphIndex];
            SetEnd(paragraphIndex, RoundTimeSpan(paragraph.End + TimeSpan.FromMilliseconds(ReplayMilliseconds)));
            FixTiming();
            changeCount++;
            mediaElement.Position = paragraph.End - TimeSpan.FromMilliseconds(ReplayMilliseconds);
            listBoxAllParagraphs.Items.Refresh();
            SaveSubtitles();
            isPlaying = true;
            mediaElement.Play();
        }

        private void buttonPrior_Click(object sender, RoutedEventArgs e)
        {
            if (paragraphIndex > 0)
            {
                paragraphIndex--;
                var paragraph = paragraphs[paragraphIndex];
                mediaElement.Position = paragraph.Start;
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
            else
            {
                var paragraph = paragraphs[paragraphIndex];
                mediaElement.Position = paragraph.Start;
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private void buttonNext_Click(object sender, RoutedEventArgs e)
        {
            if (paragraphIndex < paragraphs.Count - 1)
            {
                paragraphIndex++;
                var paragraph = paragraphs[paragraphIndex];
                mediaElement.Position = paragraph.Start;
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
            else
            {
                var prevParagraph = paragraphs[paragraphIndex];
                if (!prevParagraph.IsValid())
                    return;
                var paragraph = new Paragraph
                {
                    Start = prevParagraph.End,
                    End = RoundTimeSpan(prevParagraph.End.Add(TimeSpan.FromSeconds(MaxParagraphSeconds))),
                    Text = "...",
                };
                changeCount++;
                paragraphIndex = paragraphs.Count;
                paragraphs.Add(paragraph);
                listBoxAllParagraphs.Items.Add(paragraph);
                mediaElement.Position = paragraph.Start;
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private void mediaElement_MediaEnded(object sender, RoutedEventArgs e)
        {
            isPlaying = false;
        }

        private void buttonPlay_Click(object sender, RoutedEventArgs e)
        {
            isPlaying = !isPlaying;
            if (isPlaying)
            {
                SaveSubtitles();
                mediaElement.Play();
            }
            else
                mediaElement.Pause();
        }

        private void textBoxContent_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (textBoxContent.IsFocused)
            {
                var paragraph = paragraphs[paragraphIndex];
                paragraph.Text = textBoxContent.Text;
                changeCount++;
                listBoxAllParagraphs.Items.Refresh();
            }
        }

        private void textBoxStart_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (textBoxStart.IsFocused)
            {
                var paragraph = paragraphs[paragraphIndex];
                TimeSpan timeSpan;
                if (TimeSpan.TryParse(textBoxStart.Text, out timeSpan))
                {
                    SetStart(paragraphIndex, RoundTimeSpan(timeSpan));
                    FixTiming();
                    changeCount++;
                    listBoxAllParagraphs.Items.Refresh();
                    mediaElement.Position = paragraph.Start;
                }
            }
        }

        private void textBoxEnd_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (textBoxEnd.IsFocused)
            {
                var paragraph = paragraphs[paragraphIndex];
                TimeSpan timeSpan;
                if (TimeSpan.TryParse(textBoxEnd.Text, out timeSpan))
                {
                    SetEnd(paragraphIndex, RoundTimeSpan(timeSpan));
                    FixTiming();
                    changeCount++;
                    listBoxAllParagraphs.Items.Refresh();
                    mediaElement.Position = paragraph.End - TimeSpan.FromMilliseconds(ReplayMilliseconds);
                }
            }
        }

        private void textBoxStart_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)
            {
                buttonPlay.Focus();
                var paragraph = paragraphs[paragraphIndex];
                mediaElement.Position = paragraph.Start;
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private void textBoxEnd_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Return)
            {
                buttonPlay.Focus();
                var paragraph = paragraphs[paragraphIndex];
                mediaElement.Position = paragraph.End - TimeSpan.FromMilliseconds(ReplayMilliseconds);
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveSubtitles();
        }

        private void listBoxAllParagraphs_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (listBoxAllParagraphs.SelectedIndex != -1)
            {
                var paragraph = listBoxAllParagraphs.SelectedItem as Paragraph;
                paragraphIndex = paragraphs.IndexOf(paragraph);
                buttonPlay.Focus();
                mediaElement.Position = paragraph.Start;
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private List<Paragraph> LoadSrt(string srtFileName)
        {
            var srtLines = new List<Paragraph>();
            var lines = System.IO.File.ReadAllLines(srtFileName);
            var i = 0;
            while (i < lines.Length)
                if (string.IsNullOrWhiteSpace(lines[i]))
                    i++;
                else if (Regex.IsMatch(lines[i], @"^\d+$"))
                {
                    var index = int.Parse(lines[i]);
                    i++;
                    if (Regex.IsMatch(lines[i], @"^[0-9:\,\s\-\>]+"))
                    {
                        var gt = lines[i].Split('>');
                        var st = TimeSpan.Parse(gt[0].Replace('-', ' ').Replace(',', '.').Trim());
                        var et = TimeSpan.Parse(gt[1].Replace(',', '.').Trim());
                        var tx = "";
                        i++;
                        while (i < lines.Length)
                        {
                            var s = lines[i++];
                            if (string.IsNullOrWhiteSpace(s))
                                break;
                            tx += s + "\r\n";
                        }
                        if (string.IsNullOrWhiteSpace(tx))
                            throw new Exception("fail srt '" + st + "'");
                        srtLines.Add(new Paragraph
                        {
                            Start = st,
                            End = et,
                            Text = EncodeText(tx),
                        });
                    }
                    else
                        throw new Exception("fail srt '" + lines[i] + "'");
                }
                else
                    throw new Exception("fail srt '" + lines[i] + "'");
            return srtLines;
        }

        private string GetEquivalentSubtitleAtPosition()
        {
            CheckSrtFile();
            if (paragraphIndex < srtParagraphs.Count)
                return DecodeText(srtParagraphs[paragraphIndex].Text);
            else
                return null;
        }

        private void buttonJoin_Click(object sender, RoutedEventArgs e)
        {
            if (paragraphIndex > 0)
            {
                var p0 = paragraphs[paragraphIndex - 1];
                SetStart(paragraphIndex, p0.End);
                FixTiming();
                changeCount++;
                listBoxAllParagraphs.Items.Refresh();
                SaveSubtitles();
                isPlaying = true;
                mediaElement.Play();
            }
        }

        private void buttonRepeat_Click(object sender, RoutedEventArgs e)
        {
            isRepeating = !isRepeating;
            if (isPlaying && isRepeating)
            {
                SaveSubtitles();
                var paragraph = paragraphs[paragraphIndex];
                buttonPlay.Focus();
                mediaElement.Position = paragraph.Start;
                mediaElement.Play();
            };
        }

        private void buttonSlower_Click(object sender, RoutedEventArgs e)
        {
            if (mediaElement.SpeedRatio < 1)
                mediaElement.SpeedRatio = 1;
            else
                mediaElement.SpeedRatio = 0.5;
        }

        private void buttonBlank_Click(object sender, RoutedEventArgs e)
        {
            var p = paragraphs[paragraphIndex];
            paragraphs.Remove(p);
            changeCount++;
            SaveSubtitles();
            listBoxAllParagraphs.Items.Remove(p);
            buttonPlay.Focus();
            mediaElement.Position = AtLeastOne().Start;
        }

        private void buttonExtract_Click(object sender, RoutedEventArgs e)
        {
            if (paragraphIndex < paragraphs.Count - 1)
            {
                var next = paragraphs[paragraphIndex + 1];
                var newStart = paragraphIndex > 0 ? paragraphs[paragraphIndex - 1].End : TimeSpan.FromSeconds(0);
                SetStart(paragraphIndex + 1, newStart);
            }
            var p = paragraphs[paragraphIndex];
            paragraphs.Remove(p);
            listBoxAllParagraphs.Items.Remove(p);
            SaveSubtitles();
            listBoxAllParagraphs.Items.Refresh();
            buttonPlay.Focus();
            mediaElement.Position = AtLeastOne().Start;
        }
    }
}
