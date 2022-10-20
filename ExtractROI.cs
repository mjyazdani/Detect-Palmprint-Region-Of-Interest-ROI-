using System;
using System.Collections.Generic;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.Drawing;
using MHCodes;

namespace palmsense
{
    class Program
    {
        static void Main(string[] args)
        {
            Image<Gray, byte> img = null;
            try
            {
                new Image<Gray, byte>("d:\\005.bmp");
            }
            catch { }

            CvInvoke.cvShowImage("image", img);

            Image<Gray, byte> smoothImg = img.SmoothGaussian(5);
            smoothImg._Erode(5); // in do khat komak mikonan ke tasvir kheili monaseb bashe vase kar!
            smoothImg._Dilate(5);// in do khat komak mikonan ke tasvir kheili monaseb bashe vase kar!
            CvInvoke.cvShowImage("smooth", smoothImg);

            Image<Gray, byte> gray = smoothImg.ThresholdBinary(new Gray(25), new Gray(255)); // 25 bayad bar asase mizane roshanayie tasvir be sorate dynamic taghir kone! age meghdaresh ziad bashe be moshkel barmikhorim...

            Contour<Point> biggestCnt = new Contour<Point>(new MemStorage());
            for (Contour<Point> contours = gray.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL, new MemStorage());
              contours != null; contours = contours.HNext)
            {
                if (biggestCnt.Area < contours.Area)
                    biggestCnt = contours;
            }
            MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_SIMPLEX, 1, 1);
            double d = 0.0005;// from 1e-30 up to 0.005!!! if you use smaller values the speed of calculation will be reduced, the accuracy does not depend on this value!!!
            Point p1, p2;
            toBeShown = new Image<Bgr, byte>(gray.Bitmap);// the colored image to be shown....
            Detect(gray.Width, biggestCnt, d, out p1, out p2);
            toBeShown.Draw(d.ToString("0.00000"), ref font, new Point(100, 100), new Bgr(50, 50, 50));
            toBeShown.Draw(biggestCnt.ApproxPoly(biggestCnt.Perimeter * d), new Bgr(120, 120, 160), 2);
            toBeShown.Draw(new CircleF(p1, 10), new Bgr(0,0, 255), 2);
            toBeShown.Draw(new CircleF(p2, 10), new Bgr(0,0, 255), 2);
            toBeShown.Draw(new CircleF(p1, 02), new Bgr(0,0, 255), 2);
            toBeShown.Draw(new CircleF(p2, 02), new Bgr(0,0, 255), 2);

            CvInvoke.cvShowImage("toBeShown", toBeShown);
            CvInvoke.cvWaitKey(0);
        }

        static Image<Bgr, byte> toBeShown = null;
        static bool Detect(int picWidth,Contour<Point> contour, double approximation, out Point p1, out Point p2)
        {
            p1 = new Point(0, 0);
            p2 = new Point(0, 0);

            Contour<Point> curCnt = contour.ApproxPoly(contour.Perimeter * approximation);// only approximates this contour, not it's childs

            Point[] pts = curCnt.ToArray();


            const int DEP_MIN_DIFF = 10; // DEPARTURE minimum difference: the minimum difference to detect that there is a departure on a point: the direction of edge is changing from left to right (or vice versa)

            if (pts.Length < 3)
                return false;

            List<Point> departureEdgePoints = new List<Point>();// all the departure points, from left to right or vice versa
            List<Point> departureToLeftPoints = new List<Point>();// the points that the edge is moving from right to left
            Point lastPoint = pts[0];
            int lastDirection = 0;// left = -1, right = +1, unknown = 0
            int tempDirection = 0;
            for (int i = 0; i < pts.Length; i++)
            {
                float diff = pts[i].X - lastPoint.X;
                if (Math.Sign(diff) != 0)
                    tempDirection = Math.Sign(diff);

                if (lastDirection != 0)
                {
                    if (tempDirection == lastDirection)
                    {
                        lastPoint = pts[i];
                    }
                    else
                    {
                        if (Math.Abs(diff) > DEP_MIN_DIFF)
                        {
                            departureEdgePoints.Add(lastPoint);// add the last point to the departure edge point list
                            if (tempDirection < 0)
                                departureToLeftPoints.Add(lastPoint);
                            lastPoint = pts[i];
                            lastDirection = tempDirection;
                        }
                    }
                }
                else // direction is unknown ... . It is important to find the correct first direction of edge movements ...
                {
                    double d = ((XPointF)lastPoint).Distance(pts[i]);
                    if (d > DEP_MIN_DIFF | Math.Abs(diff) > DEP_MIN_DIFF)// if the diff were big enough, then:
                    {
                        lastPoint = pts[i];
                        lastDirection = tempDirection;
                    }
                }
            }

            //// draw departureToLeftPoints
            //foreach (Point xpf in departureToLeftPoints)//departureEdgePoints)
            //    toBeShown.Draw(new CircleF(xpf, 4), new Bgr(255, 0, 0), 2);
            //// draw departureEdgePoints
            //foreach (Point xpf in departureEdgePoints)
            //    toBeShown.Draw(new CircleF(xpf, 7), new Bgr(0, 255, 0), 2);

            if (departureToLeftPoints.Count < 3)
                return false;// at least three points should've been detected
            // every three poins, that are in the queue after each other, should be checked, so the desired points would be detected
            for (int i = 0; i < departureToLeftPoints.Count - 3; i++)
            {
                if (CheckPoints(picWidth, departureToLeftPoints[i], departureToLeftPoints[i + 1], departureToLeftPoints[i + 2]))
                {
                    p1 = departureToLeftPoints[i];
                    p2 = departureToLeftPoints[i + 2];
                    return true;
                }
            }
            return false;
        }

        static bool CheckPoints(int picWidth,Point p1, Point p2, Point p3)
        {
            const int minVerticalDist = 10; // the minimum vertical distance 
            const int minHorizDist = 80;//  the minimum horizontal distance. depends on you! bigger values makes the result more accurate, but may avoid detecting any point!
            
            if (
                p1.X < picWidth / 2 && // if the points are on leftside of picture
                p2.X < picWidth / 2 &&
                p3.X < picWidth / 2 && 
                p2.Y - p1.Y > minVerticalDist && // point2 should be under point1 and have at least minVerticalDist 
                p3.Y - p2.Y > minVerticalDist && // point3 should be under point2 and have at least minVerticalDist
                Math.Abs(p1.X - p2.X) < minHorizDist && // all points have to be near each other on X axis, because the rotation is limited!
                Math.Abs(p1.X - p3.X) < minHorizDist && // all points have to be near each other on X axis, because the rotation is limited!
                Math.Abs(p2.X - p3.X) < minHorizDist    // all points have to be near each other on X axis, because the rotation is limited!
                )
                return true;
            return false;
        }
    }
}
