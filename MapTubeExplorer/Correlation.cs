using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapTubeExplorer
{
    /// <summary>
    /// Implementation of various spatial correlation methods based on a sparse grid.
    /// </summary>
    class Correlation
    {
        /// <summary>
        /// NOT FINISHED
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public static double Simple(ref List<GridCell> X, ref List<GridCell> Y)
        {
            //if (GridCell.Match(CellX, CellY))
            //{
            //}
            return 0;
        }

        /// <summary>
        /// Spatial Bivariate Moran I using
        /// I=Sigma_i(Sigma_j(Yi*Wij*Xj))/(S0*Sqrt(Variance(Y)*Variance(X)))
        /// where S0 is the sum of all the elements in W
        /// NOTE: X[i], Y[i] and Centroids[i] MUST all reference the same spatial area i.e. all three arrays are in step
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns></returns>
        public static double SpatialBivariateMoranI(ref List<GridCell> X, ref List<GridCell> Y)
        {
            RunningStat rsX = new RunningStat();
            RunningStat rsY = new RunningStat();
            foreach (GridCell Cell in X) rsX.Push(Cell.data);
            foreach (GridCell Cell in Y) rsY.Push(Cell.data);
            double MeanX = rsX.Mean, SDX = rsX.StandardDeviation;
            double MeanY = rsY.Mean, SDY = rsY.StandardDeviation;

            double Sum = 0;
            double S0 = 0;

            //now correlate
            foreach (GridCell CellX in X)
            {
                foreach (GridCell CellY in Y)
                {
                    double dx = CellX.envelope.Centre.X - CellY.envelope.Centre.X;
                    double dy = CellX.envelope.Centre.Y - CellY.envelope.Centre.Y;
                    double dist2 = dx * dx + dy * dy;
                    double W = 1;
                    if (dist2 > 1) W = 1 / Math.Sqrt(dist2); //trap for when grid cells match location
                    Sum += ((CellY.data - MeanY) / SDY) * W * ((CellX.data - MeanX) / SDX);
                    S0 += W; //sum of all weights
                }
            }
            double I = Sum / S0;

            return I;
        }
    }
}
