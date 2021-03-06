﻿//------------------------------------------------------------------------------
//  Alberto Quesada Aranda - qa.alberto@gmail.com
//
//  Kinect P1 - Movimiento 14
//  En pie con la pierna derecha levantada (plano YZ, XY en Kinect). El ángulo
//  de la pierna debe ser un parámetro de entrada.
//
//  La función importante es < angleLeg >. Devuelve true si la posición de la 
//  pierna izquierda es la requerida en el ejercicio. Los parámetros de entada
//  son el skeleton leido, el ángulo que se desea alcanzar con el ejercicio
//  y el error permitido en grados.
//
//  El resto de cambios realizados al código original del ejemplo son cambios 
//  menores. Su único proposito es pintar del color pedido los Joints y Bones
//  dependiendo de si su posición es válida/invalida, o esta por detás/delante
//  de la posición pedida.
//
//------------------------------------------------------------------------------

namespace Microsoft.Samples.Kinect.SkeletonBasics
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using Microsoft.Kinect;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Width of output drawing
        /// </summary>
        private const float RenderWidth = 640.0f;

        /// <summary>
        /// Height of our output drawing
        /// </summary>
        private const float RenderHeight = 480.0f;

        /// <summary>
        /// Thickness of drawn joint lines
        /// </summary>
        private const double JointThickness = 3;

        /// <summary>
        /// Thickness of body center ellipse
        /// </summary>
        private const double BodyCenterThickness = 10;

        /// <summary>
        /// Thickness of clip edge rectangles
        /// </summary>
        private const double ClipBoundsThickness = 10;

        /// <summary>
        /// Brush used to draw skeleton center point
        /// </summary>
        private readonly Brush centerPointBrush = Brushes.Blue;

        /// <summary>
        /// Brush used for drawing joints that are currently tracked
        /// </summary>
        private readonly Brush trackedJointBrush = new SolidColorBrush(Color.FromArgb(255, 68, 192, 68));

        /// <summary>
        /// Brush used for drawing joints that are currently inferred
        /// I have changed this color to red to make a difference with the brush I use for draw
        /// the joints that are behind the right position
        /// </summary>
        private readonly Brush inferredJointBrush = Brushes.Red;

        // brush used for drawing joints that are tracked and in the right position
        private readonly Brush trackedValidJointBrush = Brushes.Green;

        // brush used for drawing joints that are tracked and behind the right position
        private readonly Brush trackedBehJointBrush = Brushes.Yellow;

        // brush used for drawing joints that are tracken and ahead the right position. Color = turquoise.
        private readonly Brush trackedAheadJointBrush = new SolidColorBrush(Color.FromRgb(93, 193, 185));

        /// <summary>
        /// Pen used for drawing bones that are currently tracked
        /// </summary>
        private readonly Pen trackedBonePen = new Pen(Brushes.Green, 6);

        //if the position is invalid, draw the bones in red
        private readonly Pen invalidPositionPen = new Pen(Brushes.Red, 6);

        /// <summary>
        /// Pen used for drawing bones that are currently inferred
        /// </summary>
        private readonly Pen inferredBonePen = new Pen(Brushes.Gray, 1);

        /// <summary>
        /// Active Kinect sensor
        /// </summary>
        private KinectSensor sensor;

        /// <summary>
        /// Drawing group for skeleton rendering output
        /// </summary>
        private DrawingGroup drawingGroup;

        /// <summary>
        /// Drawing image that we will display
        /// </summary>
        private DrawingImage imageSource;

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Draws indicators to show which edges are clipping skeleton data
        /// </summary>
        /// <param name="skeleton">skeleton to draw clipping information for</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private static void RenderClippedEdges(Skeleton skeleton, DrawingContext drawingContext)
        {
            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Bottom))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, RenderHeight - ClipBoundsThickness, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Top))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, RenderWidth, ClipBoundsThickness));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Left))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(0, 0, ClipBoundsThickness, RenderHeight));
            }

            if (skeleton.ClippedEdges.HasFlag(FrameEdges.Right))
            {
                drawingContext.DrawRectangle(
                    Brushes.Red,
                    null,
                    new Rect(RenderWidth - ClipBoundsThickness, 0, ClipBoundsThickness, RenderHeight));
            }
        }

        /// <summary>
        /// Execute startup tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            // Create the drawing group we'll use for drawing
            this.drawingGroup = new DrawingGroup();

            // Create an image source that we can use in our image control
            this.imageSource = new DrawingImage(this.drawingGroup);

            // Display the drawing using our image control
            Image.Source = this.imageSource;

            // Look through all sensors and start the first connected one.
            // This requires that a Kinect is connected at the time of app startup.
            // To make your app robust against plug/unplug,
            // it is recommended to use KinectSensorChooser provided in Microsoft.Kinect.Toolkit (See components in Toolkit Browser).
            foreach (var potentialSensor in KinectSensor.KinectSensors)
            {
                if (potentialSensor.Status == KinectStatus.Connected)
                {
                    this.sensor = potentialSensor;
                    break;
                }
            }

            if (null != this.sensor)
            {
                // Turn on the skeleton stream to receive skeleton frames
                this.sensor.SkeletonStream.Enable();

                // Add an event handler to be called whenever there is new color frame data
                this.sensor.SkeletonFrameReady += this.SensorSkeletonFrameReady;

                // Start the sensor!
                try
                {
                    this.sensor.Start();
                }
                catch (IOException)
                {
                    this.sensor = null;
                }
            }

            if (null == this.sensor)
            {
                this.statusBarText.Text = Properties.Resources.NoKinectReady;
            }
        }

        /// <summary>
        /// Execute shutdown tasks
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (null != this.sensor)
            {
                this.sensor.Stop();
            }
        }

        /// <summary>
        /// Event handler for Kinect sensor's SkeletonFrameReady event
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void SensorSkeletonFrameReady(object sender, SkeletonFrameReadyEventArgs e)
        {
            Skeleton[] skeletons = new Skeleton[0];

            using (SkeletonFrame skeletonFrame = e.OpenSkeletonFrame())
            {
                if (skeletonFrame != null)
                {
                    skeletons = new Skeleton[skeletonFrame.SkeletonArrayLength];
                    skeletonFrame.CopySkeletonDataTo(skeletons);
                }
            }

            using (DrawingContext dc = this.drawingGroup.Open())
            {
                // Draw a transparent background to set the render size
                dc.DrawRectangle(Brushes.Black, null, new Rect(0.0, 0.0, RenderWidth, RenderHeight));

                if (skeletons.Length != 0)
                {
                    foreach (Skeleton skel in skeletons)
                    {
                        RenderClippedEdges(skel, dc);

                        if (skel.TrackingState == SkeletonTrackingState.Tracked)
                        {
                            this.DrawBonesAndJoints(skel, dc);
                        }
                        else if (skel.TrackingState == SkeletonTrackingState.PositionOnly)
                        {
                            dc.DrawEllipse(
                            this.centerPointBrush,
                            null,
                            this.SkeletonPointToScreen(skel.Position),
                            BodyCenterThickness,
                            BodyCenterThickness);
                        }
                    }
                }

                // prevent drawing outside of our render area
                this.drawingGroup.ClipGeometry = new RectangleGeometry(new Rect(0.0, 0.0, RenderWidth, RenderHeight));
            }
        }

        /// <summary>
        /// Check position of the left leg
        /// </summary>
        /// <param name="skeleton">skeleton to check</param>
        /// <param name="angle">angle searched</param>
        /// <param name="allowed_error">error allowed in the comprobation</param>
        private bool angleLeg(Skeleton skeleton, double angle, double allowed_error) {

            //return variable - true if position is valid, false if invalid
            bool check = true;

            float distA, distB;
            distA = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.Y - skeleton.Joints[JointType.AnkleLeft].Position.Y);
            distB = System.Math.Abs(skeleton.Joints[JointType.HipCenter].Position.X - skeleton.Joints[JointType.AnkleLeft].Position.X);

            // the angle I'm looking for is the formed between the HipCenter, the floor and the AnkleLeft
            // I know the position of the HipCenter and the AnkleLeft so it can be calculed by
            // arctang[(Hip.X-Ank.X) / (Hip.Y-Ank.Y)]
            double segmentAngle = Math.Atan(distB/distA);
            
            // With this operation I transform the angle to degrees
            double degrees = segmentAngle * (180 / Math.PI);
            degrees = degrees % 360;

            // Check if the actual angle is equal to the angle specified
            if(Math.Abs(angle - degrees) < allowed_error)
                check = true;
            else
                check = false;

            return check;

        }

        /// <summary>
        /// Draws a skeleton's bones and joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        private void DrawBonesAndJoints(Skeleton skeleton, DrawingContext drawingContext)
        {
            // Render Torso
            this.DrawBone(skeleton, drawingContext, JointType.Head, JointType.ShoulderCenter);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.ShoulderRight);
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderCenter, JointType.Spine);
            this.DrawBone(skeleton, drawingContext, JointType.Spine, JointType.HipCenter);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipLeft);
            this.DrawBone(skeleton, drawingContext, JointType.HipCenter, JointType.HipRight);

        /*

            //This is the control for both arms -> It will check if the arms are open and straight.
            //I haven't try this part actually. It is only implemented because at first I thought
            //I had to do it, but no.

            //-----------------------------------------------------------------------
            // Control of the right arm

            bool elbowRY = (skeleton.Joints[JointType.ShoulderRight].Position.Y - skeleton.Joints[JointType.ElbowRight].Position.Y < 0.5);
            bool elbowRZ = (skeleton.Joints[JointType.ShoulderRight].Position.Z - skeleton.Joints[JointType.ElbowRight].Position.Z < 0.5);
            if(elbowRY && elbowRZ)
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            else
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight, 0);

            bool wristRY = (skeleton.Joints[JointType.ElbowRight].Position.Y - skeleton.Joints[JointType.WristRight].Position.Y < 0.5);
            bool wristRZ = (skeleton.Joints[JointType.ElbowRight].Position.Z - skeleton.Joints[JointType.WristRight].Position.Z < 0.5);
            if(wristRY && wristRZ)
                this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            else
                this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight, 0);

            bool handRY = (skeleton.Joints[JointType.WristRight].Position.Y - skeleton.Joints[JointType.HandRight].Position.Y < 0.5);
            bool handRZ = (skeleton.Joints[JointType.WristRight].Position.Z - skeleton.Joints[JointType.HandRight].Position.Z < 0.5);
            if(handRY && handRZ)
                this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);
            else
                this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight, 0);

            //-----------------------------------------------------------------------

            //-----------------------------------------------------------------------
            // Control of the left arm

            bool elbowLY = (skeleton.Joints[JointType.ShoulderLeft].Position.Y - skeleton.Joints[JointType.ElbowLeft].Position.Y < 0.5);
            bool elbowLZ = (skeleton.Joints[JointType.ShoulderLeft].Position.Z - skeleton.Joints[JointType.ElbowLeft].Position.Z < 0.5);
            if(elbowLY && elbowLZ)
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            else
                this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft, 0);

            bool wristLY = (skeleton.Joints[JointType.ElbowLeft].Position.Y - skeleton.Joints[JointType.WristLeft].Position.Y < 0.5);
            bool wristLZ = (skeleton.Joints[JointType.ElbowLeft].Position.Z - skeleton.Joints[JointType.WristLeft].Position.Z < 0.5);
            if(wristLY && wristLZ)
                this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            else
                this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft, 0);

            bool handLY = (skeleton.Joints[JointType.WristLeft].Position.Y - skeleton.Joints[JointType.HandLeft].Position.Y < 0.5);
            bool handLZ = (skeleton.Joints[JointType.WristLeft].Position.Z - skeleton.Joints[JointType.HandLeft].Position.Z < 0.5);
            if(handLY && handLZ)
                this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);
            else
                this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft, 0);
        */

            // Left Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderLeft, JointType.ElbowLeft);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowLeft, JointType.WristLeft);
            this.DrawBone(skeleton, drawingContext, JointType.WristLeft, JointType.HandLeft);

            // Right Arm
            this.DrawBone(skeleton, drawingContext, JointType.ShoulderRight, JointType.ElbowRight);
            this.DrawBone(skeleton, drawingContext, JointType.ElbowRight, JointType.WristRight);
            this.DrawBone(skeleton, drawingContext, JointType.WristRight, JointType.HandRight);


            //-----------------------------------------------------------------------
            // Control of the left leg

            // Angle for the exercise and error allowed
            double angle = 40, allowed_error = angle*0.05;
            bool legPos = angleLeg(skeleton, angle, allowed_error);

            // Left Leg 
            // If the position of the left leg is the one we were looking for, the draw the leg in green
            if(legPos && (skeleton.Joints[JointType.AnkleLeft].Position.Z - skeleton.Joints[JointType.AnkleRight].Position.Z < 0.05)) {
                this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft);
                this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft);
            }
            // Else, draw the leg in red
            else {
                this.DrawBone(skeleton, drawingContext, JointType.HipLeft, JointType.KneeLeft, 0);
                this.DrawBone(skeleton, drawingContext, JointType.KneeLeft, JointType.AnkleLeft, 0);
                this.DrawBone(skeleton, drawingContext, JointType.AnkleLeft, JointType.FootLeft, 0);
            }


            // 
            //-----------------------------------------------------------------------


            // Right Leg
            this.DrawBone(skeleton, drawingContext, JointType.HipRight, JointType.KneeRight);
            this.DrawBone(skeleton, drawingContext, JointType.KneeRight, JointType.AnkleRight);
            this.DrawBone(skeleton, drawingContext, JointType.AnkleRight, JointType.FootRight);

            // Render Joints
            // I have to change this function to include the new colors for the diferent position of the joints
            foreach (Joint joint in skeleton.Joints)
            {
                Brush drawBrush = null;

                if (joint.TrackingState == JointTrackingState.Tracked)
                {
                    drawBrush = this.trackedJointBrush;
                }
                else if (joint.TrackingState == JointTrackingState.Inferred)
                {
                    drawBrush = this.inferredJointBrush;
                }

                //// Change color of the left leg Joint depending on its position.
                // I've comented it because I check it better without change colors, but it's working right.
                /*
                if (joint == skeleton.Joints[JointType.AnkleLeft])
                {
                    if (skeleton.Joints[JointType.AnkleLeft].Position.Z - skeleton.Joints[JointType.AnkleRight].Position.Z < 0.2)
                    {
                        drawBrush = this.trackedValidJointBrush;
                    }
                    else if (skeleton.Joints[JointType.AnkleLeft].Position.Z < skeleton.Joints[JointType.AnkleRight].Position.Z)
                    {
                        drawBrush = this.trackedBehJointBrush;

                    }
                    else
                    {
                        drawBrush = this.trackedAheadJointBrush;
                    }
                }
                if (joint == skeleton.Joints[JointType.FootLeft])
                {
                    if (skeleton.Joints[JointType.FootLeft].Position.Z - skeleton.Joints[JointType.FootLeft].Position.Z < 0.2)
                    {
                        drawBrush = this.trackedValidJointBrush;
                    }
                    else if (skeleton.Joints[JointType.FootLeft].Position.Z < skeleton.Joints[JointType.FootLeft].Position.Z)
                    {
                        drawBrush = this.trackedBehJointBrush;

                    }
                    else
                    {
                        drawBrush = this.trackedAheadJointBrush;
                    }
                }
                if (joint == skeleton.Joints[JointType.KneeLeft])
                {
                    if (skeleton.Joints[JointType.KneeLeft].Position.Z - skeleton.Joints[JointType.KneeLeft].Position.Z < 0.2)
                    {
                        drawBrush = this.trackedValidJointBrush;
                    }
                    else if (skeleton.Joints[JointType.KneeLeft].Position.Z < skeleton.Joints[JointType.KneeLeft].Position.Z)
                    {
                        drawBrush = this.trackedBehJointBrush;

                    }
                    else
                    {
                        drawBrush = this.trackedAheadJointBrush;
                    }
                }*/

                if (drawBrush != null)
                {
                    drawingContext.DrawEllipse(drawBrush, null, this.SkeletonPointToScreen(joint.Position), JointThickness, JointThickness);
                }
            }
        }

        /// <summary>
        /// Maps a SkeletonPoint to lie within our render space and converts to Point
        /// </summary>
        /// <param name="skelpoint">point to map</param>
        /// <returns>mapped point</returns>
        private Point SkeletonPointToScreen(SkeletonPoint skelpoint)
        {
            // Convert point to depth space.
            // We are not using depth directly, but we do want the points in our 640x480 output resolution.
            DepthImagePoint depthPoint = this.sensor.CoordinateMapper.MapSkeletonPointToDepthPoint(skelpoint, DepthImageFormat.Resolution640x480Fps30);
            return new Point(depthPoint.X, depthPoint.Y);
        }

        /// <summary>
        /// Draws a bone line between two joints
        /// </summary>
        /// <param name="skeleton">skeleton to draw bones from</param>
        /// <param name="drawingContext">drawing context to draw to</param>
        /// <param name="jointType0">joint to start drawing from</param>
        /// <param name="jointType1">joint to end drawing at</param>
        /// I have added a new parameter. It indicates if the bone should be draw in green or red, depending on its position.
        /// The default value is 1 which indicates that the color should be green (valid position).
        private void DrawBone(Skeleton skeleton, DrawingContext drawingContext, JointType jointType0, JointType jointType1, int valid = 1)
        {
            Joint joint0 = skeleton.Joints[jointType0];
            Joint joint1 = skeleton.Joints[jointType1];

            // If we can't find either of these joints, exit
            if (joint0.TrackingState == JointTrackingState.NotTracked ||
                joint1.TrackingState == JointTrackingState.NotTracked)
            {
                return;
            }

            // Don't draw if both points are inferred
            if (joint0.TrackingState == JointTrackingState.Inferred &&
                joint1.TrackingState == JointTrackingState.Inferred)
            {
                return;
            }

            // We assume all drawn bones are inferred unless BOTH joints are tracked
            Pen drawPen = this.inferredBonePen;
            if (joint0.TrackingState == JointTrackingState.Tracked && joint1.TrackingState == JointTrackingState.Tracked)
            {
              if(valid == 1)
                drawPen = this.trackedBonePen;
              else
                drawPen = this.invalidPositionPen;
            }

            drawingContext.DrawLine(drawPen, this.SkeletonPointToScreen(joint0.Position), this.SkeletonPointToScreen(joint1.Position));
        }

        /// <summary>
        /// Handles the checking or unchecking of the seated mode combo box
        /// </summary>
        /// <param name="sender">object sending the event</param>
        /// <param name="e">event arguments</param>
        private void CheckBoxSeatedModeChanged(object sender, RoutedEventArgs e)
        {
            if (null != this.sensor)
            {
                if (this.checkBoxSeatedMode.IsChecked.GetValueOrDefault())
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Seated;
                }
                else
                {
                    this.sensor.SkeletonStream.TrackingMode = SkeletonTrackingMode.Default;
                }
            }
        }
    }
}
