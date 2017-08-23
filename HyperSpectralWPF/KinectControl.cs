using System;
using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using Microsoft.Kinect;

namespace HyperSpectralWPF
{
    /// <summary>
    /// 
    /// </summary>
    class KinectControl
    {
        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        KinectSensor sensor;

        /// <summary>
        /// Reader for body frames
        /// </summary>
        BodyFrameReader bodyFrameReader;
        
        /// <summary>
        /// Array for the bodies
        /// </summary>
        private Body[] bodies = null;
        
        /// <summary>
        /// Screen width and height for determining the exact mouse sensitivity
        /// </summary>
        int screenWidth, screenHeight;

        /// <summary>
        /// Timer for pause-to-click feature
        /// </summary>
        DispatcherTimer timer = new DispatcherTimer();

        /// <summary>
        /// How far the cursor move according to your hand's movement
        /// </summary>
        public float mouseSensitivity = MOUSE_SENSITIVITY;

        /// <summary>
        /// Time required as a pause-clicking
        /// </summary>
        public float timeRequired = TIME_REQUIRED;
        
        /// <summary>
        /// The radius range your hand move inside a circle for [timeRequired] seconds would be regarded as a pause-clicking
        /// </summary>
        public float pauseThresold = PAUSE_THRESOLD;
        
        /// <summary>
        /// Decide if the user need to do clicks or only move the cursor
        /// </summary>
        public bool doClick = DO_CLICK;
        
        /// <summary>
        /// Use Grip gesture to click or not
        /// </summary>
        public bool useGripGesture = USE_GRIP_GESTURE;
        
        /// <summary>
        /// Value 0 - 0.95f, the larger it is, the smoother the cursor would move
        /// </summary>
        public float cursorSmoothing = CURSOR_SMOOTHING;

        // Default values
        public const bool  DO_CLICK          = true;
        public const bool  USE_GRIP_GESTURE  = true;
        public const float MOUSE_SENSITIVITY = 3.5f;
        public const float TIME_REQUIRED     = 2f;
        public const float PAUSE_THRESOLD    = 60f;
        public const float CURSOR_SMOOTHING  = 0.2f;

        /// <summary>
        /// Determine if we have tracked the hand and used it to move the cursor,
        /// If false, meaning the user may not lift their hands, we don't get the last hand position and some actions like pause-to-click won't be executed.
        /// </summary>
        bool alreadyTrackedPosition = false;

        /// <summary>
        /// for storing the time passed for pause-to-click
        /// </summary>
        float timeCount = 0;
        
        /// <summary>
        /// For storing last cursor position
        /// </summary>
        Point lastCursorPosition = new Point(0, 0);

        /// <summary>
        /// If true, user did a left hand Grip gesture
        /// </summary>
        bool wasLeftGrip = false;
        
        /// <summary>
        /// If true, user did a right hand Grip gesture
        /// </summary>
        bool wasRightGrip = false;

        public KinectControl()
        {
            // Get active Kinect Sensor
            sensor = KinectSensor.GetDefault();

            // Open the reader for the body frames
            bodyFrameReader = sensor.BodyFrameSource.OpenReader();
            bodyFrameReader.FrameArrived += bodyFrameReader_FrameArrived;

            // Get screen with and height
            screenWidth  = (int)SystemParameters.PrimaryScreenWidth;
            screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            // Set up timer, execute every 0.1s
            timer.Interval = new TimeSpan(0, 0, 0, 0, 100);
            timer.Tick += new EventHandler(Timer_Tick);
            timer.Start();

            // Open the sensor
            sensor.Open();
        }
        
        /// <summary>
        /// Pause to click timer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void Timer_Tick(object sender, EventArgs e)
        {
            if (!doClick || useGripGesture) return;

            if (!alreadyTrackedPosition)
            {
                timeCount = 0;
                return;
            }

            Point cursorPosition = MouseControl.GetCursorPosition();

            if ((lastCursorPosition - cursorPosition).Length < pauseThresold)
            {
                if ((timeCount += 0.1f) > timeRequired)
                {
                    //MouseControl.MouseLeftDown();
                    //MouseControl.MouseLeftUp();
                    MouseControl.DoMouseClick();
                    timeCount = 0;
                }
            }
            else
            {
                timeCount = 0;
            }

            lastCursorPosition = cursorPosition;
        }

        /// <summary>
        /// Read body frames
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void bodyFrameReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            bool dataReceived = false;

            using (BodyFrame bodyFrame = e.FrameReference.AcquireFrame())
            {
                if (bodyFrame != null)
                {
                    if (this.bodies == null)
                    {
                        this.bodies = new Body[bodyFrame.BodyCount];
                    }

                    // The first time GetAndRefreshBodyData is called, Kinect will allocate each Body in the array.
                    // As long as those body objects are not disposed and not set to null in the array,
                    // those body objects will be re-used.
                    bodyFrame.GetAndRefreshBodyData(this.bodies);
                    dataReceived = true;
                }
            }

            if (!dataReceived)
            {
                alreadyTrackedPosition = false;
                return;
            }

            foreach (Body body in this.bodies)
            {
                // Get first tracked body only, notice there's a break below.
                if (body.IsTracked)
                {
                    // Get various skeletal positions
                    CameraSpacePoint handLeft  = body.Joints[JointType.HandLeft].Position;
                    CameraSpacePoint handRight = body.Joints[JointType.HandRight].Position;
                    CameraSpacePoint spineBase = body.Joints[JointType.SpineBase].Position;

                    if (handRight.Z - spineBase.Z < -0.15f) // if right hand lift forward
                    {
                        /* Hand x calculated by this. we don't use shoulder right as a reference cause the shoulder right
                         * is usually behind the lift right hand, and the position would be inferred and unstable.
                         * because the spine base is on the left of right hand, we plus 0.05f to make it closer to the right. */
                        float x = handRight.X - spineBase.X + 0.05f;
                        
                        /* Hand y calculated by this. As spine base is way lower than right hand, we plus 0.51f to make it
                         * higher, the value 0.51f is worked out by testing for a several times, you can set it as another one you like. */
                        float y = spineBase.Y - handRight.Y + 0.51f;
                        
                        // Get current cursor position
                        Point cursorPosition = MouseControl.GetCursorPosition();
                        
                        // Smoothing for using should be 0 - 0.95f. The way we smooth the cusor is: oldPos + (newPos - oldPos) * smoothValue
                        float smoothing = 1 - cursorSmoothing;
                        
                        // Set cursor position
                        MouseControl.SetCursorPos((int)(cursorPosition.X + (x * mouseSensitivity * screenWidth - cursorPosition.X) * smoothing), (int)(cursorPosition.Y + ((y + 0.25f) * mouseSensitivity * screenHeight - cursorPosition.Y) * smoothing));

                        alreadyTrackedPosition = true;

                        // Grip gesture
                        if (doClick && useGripGesture)
                        {
                            if (body.HandRightState == HandState.Closed)
                            {
                                if (!wasRightGrip)
                                {
                                    MouseControl.MouseLeftDown();
                                    wasRightGrip = true;
                                }
                            }
                            else if (body.HandRightState == HandState.Open)
                            {
                                if (wasRightGrip)
                                {
                                    MouseControl.MouseLeftUp();
                                    wasRightGrip = false;
                                }
                            }
                        }
                    }
                    else if (handLeft.Z - spineBase.Z < -0.15f) // If left hand lift forward
                    {
                        float x = handLeft.X - spineBase.X + 0.3f;
                        float y = spineBase.Y - handLeft.Y + 0.51f;
                        Point curPos = MouseControl.GetCursorPosition();
                        float smoothing = 1 - cursorSmoothing;
                        MouseControl.SetCursorPos((int)(curPos.X + (x * mouseSensitivity * screenWidth - curPos.X) * smoothing), (int)(curPos.Y + ((y + 0.25f) * mouseSensitivity * screenHeight - curPos.Y) * smoothing));
                        alreadyTrackedPosition = true;

                        if (doClick && useGripGesture)
                        {
                            if (body.HandLeftState == HandState.Closed)
                            {
                                if (!wasLeftGrip)
                                {
                                    MouseControl.MouseLeftDown();
                                    wasLeftGrip = true;
                                }
                            }
                            else if (body.HandLeftState == HandState.Open)
                            {
                                if (wasLeftGrip)
                                {
                                    MouseControl.MouseLeftUp();
                                    wasLeftGrip = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        wasLeftGrip = true;
                        wasRightGrip = true;
                        alreadyTrackedPosition = false;
                    }

                    // Get first tracked body only
                    break;
                }
            }
        }

        public void Close()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }

            if (this.sensor != null)
            {
                this.sensor.Close();
                this.sensor = null;
            }
        }
    }
}
