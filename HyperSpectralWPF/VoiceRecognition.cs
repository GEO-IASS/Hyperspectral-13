using Microsoft.Kinect;
using Microsoft.Speech.AudioFormat;
using Microsoft.Speech.Recognition;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Handles all functions related to voice recognition
    /// </summary>
    class VoiceRecognition
    {
        /// <summary>
        /// Variables pertaining to voice recognition
        /// </summary>
        private KinectSensor            kinectSensor  = null;
        private KinectAudioStream       convertStream = null;
        private SpeechRecognitionEngine speechEngine  = null;
        private MainWindow              mainWindow    = null;
        private bool                    enabled       = true;

        /// <summary>
        /// Constructor for voice recognition
        /// </summary>
        /// <param name="kinectSensor"></param>
        /// <param name="mainWindow"></param>
        public VoiceRecognition(KinectSensor kinectSensor, MainWindow mainWindow)
        {
            this.kinectSensor = kinectSensor;
            this.mainWindow = mainWindow;

            // Grab the audio stream
            IReadOnlyList<AudioBeam> audioBeamList = kinectSensor.AudioSource.AudioBeams;
            Stream audioStream = audioBeamList[0].OpenInputStream();

            // Create the convert stream
            convertStream = new KinectAudioStream(audioStream);

            RecognizerInfo recognizerInfo = TryGetKinectRecognizer();

            if (recognizerInfo != null)
            {
                speechEngine = new SpeechRecognitionEngine(recognizerInfo.Id);

                Choices indices = new Choices();
                GrammarBuilder indexValues = new GrammarBuilder { Culture = recognizerInfo.Culture };
                GrammarBuilder imageNavigationSpeech = new GrammarBuilder();

                for (int i = 0; i < 78; i++)
                {
                    SemanticResultValue index = new SemanticResultValue(IntToWord.IntegerToWritten(i + 1), i + 1);
                    indices.Add(index);
                    indexValues.Append(index);
                }

                indexValues.Append(indices);
                imageNavigationSpeech.Append("go to image");
                imageNavigationSpeech.Append(new SemanticResultKey("imageNumber", indices));

                Grammar imageNavigationGrammar = new Grammar(imageNavigationSpeech);
                imageNavigationGrammar.Name = "ImageNavigation";

                speechEngine.LoadGrammar(imageNavigationGrammar);

                // Create a grammar from grammar definition XML file.
                using (var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(Properties.Resources.SpeechGrammar)))
                {
                    Grammar XMLGrammar = new Grammar(memoryStream);
                    XMLGrammar.Name = "XMLGrammar";
                    speechEngine.LoadGrammar(XMLGrammar);
                }

                speechEngine.SpeechRecognized += SpeechRecognized;
                speechEngine.SpeechRecognitionRejected += SpeechRejected;

                // let the convertStream know speech is going active
                convertStream.SpeechActive = true;

                // For long recognition sessions (a few hours or more), it may be beneficial to turn off adaptation of the acoustic model. 
                // This will prevent recognition accuracy from degrading over time.
                speechEngine.UpdateRecognizerSetting("AdaptationOn", 0);

                speechEngine.SetInputToAudioStream(this.convertStream, new SpeechAudioFormatInfo(EncodingFormat.Pcm, 16000, 16, 1, 32000, 2, null));
                speechEngine.RecognizeAsync(RecognizeMode.Multiple);
            }
        }

        /// <summary>
        /// Handler for recognized speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            // Speech utterance confidence below which we treat speech as if it hadn't been heard
            const double ConfidenceThreshold = 0.3;

            if (e.Result.Confidence >= ConfidenceThreshold && enabled)
            {
                switch (e.Result.Grammar.Name)
                {
                    case "ImageNavigation":
                        int requestedImageIndex = (int)e.Result.Semantics["imageNumber"].Value;

                        Console.WriteLine("Speech Recognized: " + e.Result.Text);
                        Console.WriteLine("Semantic Results: " + requestedImageIndex);
                        Console.WriteLine("Speech Result Confidence: " + e.Result.Confidence);

                        string a = e.Result.Text.ToLowerInvariant();
                        string b = "go to image " + IntToWord.IntegerToWritten(requestedImageIndex);
                        if (SpeechAnalyzer.Levenshtein(a, b) == 0)
                        {
                            mainWindow.GetImageManager().GoToImage(requestedImageIndex);
                        }
                        break;
                    case "XMLGrammar":
                        Console.WriteLine("Speech Recognized: " + e.Result.Text);
                        Console.WriteLine("Speech Result Confidence: " + e.Result.Confidence);

                        switch (e.Result.Semantics.Value.ToString())
                        {
                            case "FORWARD":
                                mainWindow.GetImageManager().ShowNextImage();
                                break;

                            case "BACKWARD":
                                mainWindow.GetImageManager().ShowPreviousImage();
                                break;
                        }
                        break;
                }

            }
        }

        /// <summary>
        /// Handler for rejected speech events.
        /// </summary>
        /// <param name="sender">object sending the event.</param>
        /// <param name="e">event arguments.</param>
        private void SpeechRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
        }

        /// <summary>
        /// Gets the metadata for the speech recognizer (acoustic model) most suitable to
        /// process audio from Kinect device.
        /// </summary>
        /// <returns>
        /// RecognizerInfo if found, <code>null</code> otherwise.
        /// </returns>
        private static RecognizerInfo TryGetKinectRecognizer()
        {
            IEnumerable<RecognizerInfo> recognizers;

            // This is required to catch the case when an expected recognizer is not installed.
            // By default - the x86 Speech Runtime is always expected. 
            try
            {
                recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            }
            catch (COMException)
            {
                return null;
            }

            foreach (RecognizerInfo recognizer in recognizers)
            {
                string value;
                recognizer.AdditionalInfo.TryGetValue("Kinect", out value);
                if ("True".Equals(value, StringComparison.OrdinalIgnoreCase) && "en-US".Equals(recognizer.Culture.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return recognizer;
                }
            }

            return null;
        }

        /// <summary>
        /// Closes the speech engine and convert stream.
        /// </summary>
        public void Close()
        {
            if (null != this.convertStream)
            {
                this.convertStream.SpeechActive = false;
            }

            if (null != this.speechEngine)
            {
                this.speechEngine.SpeechRecognized -= this.SpeechRecognized;
                this.speechEngine.SpeechRecognitionRejected -= this.SpeechRejected;
                this.speechEngine.RecognizeAsyncStop();
            }
        }

        /// <summary>
        /// Used to check if voice recognition is enabled or not.
        /// </summary>
        /// <returns>enabled status</returns>
        public bool IsEnabled()
        {
            return enabled;
        }

        /// <summary>
        /// Enables voice recognition.
        /// </summary>
        public void Enable()
        {
            enabled = true;
        }

        /// <summary>
        /// Disables voice recognition
        /// </summary>
        public void Disable()
        {
            enabled = false;
        }
    }
}
