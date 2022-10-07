private void FillBlob(Mat src)
{
	using(Mat gray = src.CvtColor(ColorConversionCodes.BGR2GRAY))
	using(Mat binary = gray.Threshold(128, 255, ThresholdTypes.Binary))
	{
		// draw contours
		OpenCvSharp.Point[][] contours;
		OpenCvSharp.HierarchyIndex[] hierarchyIndexes;
		binary.FindContours(out contours, out hierarchyIndexes, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);
		List<OpenCvSharp.Point[]> contours_filtered = new List<OpenCvSharp.Point[]>();  // 用于存储根据条件筛选过后的区域对象

		// 用于筛选blob尺寸的条件，跟ConnectedComponents里面的Width， Height一样
		int h_lower = 0;
		int h_upper = 1000;
		int w_lower = 0;
		int w_upper = 1000;

		foreach (OpenCvSharp.Point[] c in contours)
		{
			RotatedRect minRect = Cv2.MinAreaRect(c);
			// 根据高、宽筛选
			if (minRect.Size.Height >= h_lower && minRect.Size.Height <= h_upper
					&& minRect.Size.Width >= w_lower && minRect.Size.Width <= w_upper)
			{
				contours_filtered.Add(c);
			}
		}
		// 填充区域(第5个参数thickness设置为-1时表示填充, 大于0时仅在边缘描出厚度为thickness的线）
		Cv2.DrawContours(src, contours_filtered, -1, Scalar.Blue, -1);
	}

}





/// <summary>
/// 输入轮廓，返回圆度
/// </summary>
/// <param name="c"></param>
/// <returns></returns>
public double ContourCircularity(Point[] c)
{

    Point centroid = ContourCentroid(c);//质心,,很接近Blob质心
    double cx = centroid.X;
    double cy = centroid.Y;
    double d = 0;
    double f = c.Count();//找轮廓面积
    foreach (Point p in c)
    {
        d += Math.Sqrt(Math.Pow((p.X - cx), 2) + Math.Pow((p.Y - cy), 2));
    }
    double distance = d / f;
    double ds = 0;
    foreach (Point p in c)
    {
        ds += Math.Pow((Math.Sqrt(Math.Pow((p.X - cx), 2) + Math.Pow((p.Y - cy), 2)) - distance), 2);

    }
    double sigma2 = ds / f;
    double roundness = 1 - Math.Sqrt(sigma2) / distance;
    return roundness;

}
/// <summary>
/// 输入轮廓，输出质心
/// </summary>
/// <param name="c"></param>
/// <returns></returns>
public Point ContourCentroid(Point[] c)
{
    Moments m = Cv2.Moments(c);
    double cx = m.M10 / m.M00;//质心,,很接近Blob质心
    double cy = m.M01 / m.M00;
    return new Point(cx, cy);
}

/// <summary>
/// 填充指定圆轮廓，画轮廓，返回指定质心集合
/// </summary>
/// <param name="src"></param>
/// <param name="canvas"></param>
/// <param name="diameter"></param>
/// <param name="range"></param>
/// <param name="roundness_lower"></param>
/// <returns></returns>
public List<Point> FillBlobFilterByCircularity(Mat src, Mat canvas, int diameter, int range, float roundness_lower)
{
    logger.Info(DateTime.Now.ToString("mm-ss-fff"));
    //List<ConnectedComponents.Blob> targetBlobs = new List<ConnectedComponents.Blob>();
    src.FindContours(out Point[][] contours, out HierarchyIndex[] hierarchyIndexes, RetrievalModes.Tree, ContourApproximationModes.ApproxSimple);//查找边缘
    List<Point[]> contours_filtered = new List<Point[]>();  // 用于存储根据条件筛选过后的区域对象
    List<Point> target_centroids = new List<Point>();

    foreach (Point[] c in contours)
    {
        RotatedRect minRect = Cv2.MinAreaRect(c);
        //logger.Info("+++++" + ContourCircularity(c));
        //logger.Info("+++" + minRect.Size.Height+"WWWW:"+ minRect.Size.Width);

        // 根据高、宽筛选
        if (Math.Abs(minRect.Size.Height - diameter) <= range
                && Math.Abs(minRect.Size.Width - diameter) <= range)
        {
            if (ContourCircularity(c) > roundness_lower)//圆度大于这个值时候，0.8
            {
                contours_filtered.Add(c);
                target_centroids.Add(ContourCentroid(c));

                logger.Debug("ContourCircularity:------" + ContourCircularity(c));
                logger.Debug("ContourCentroid:------" + ContourCentroid(c));

            }
        }
    }

    Cv2.FillPoly(src, contours_filtered, Scalar.White);//填充
    Cv2.DrawContours(canvas, contours_filtered, -1, Scalar.GreenYellow, 1);//轮廓点画在图上

    //logger.Info(DateTime.Now.ToString("mm-ss-fff"));

    return target_centroids;
}




