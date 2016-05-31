using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

/// <summary>
/// MapTube Explorer
/// Author: Richard Milton
/// Date: 28 May 2016
/// https://github.com/MapTube/MapTubeExplorer
/// Command line program to load two maps from MapTube using the shapefile download web service and perform a grid correlation.
/// This is intended to be the template for more complex experiments on map comparisons using the data held on MapTube.
/// 
/// An interesting addition might be to add the ability to do comparisons with WFS sources like from data.gov.uk.
/// 
/// Install-Package NetTopologySuite
/// Install-Package NetTopologySuite.IO
/// This gets you GeoAPI as well.
/// </summary>

namespace MapTubeExplorer
{
    class Program
    {
        //2725 is pop density from 2011 Census QS102EW0003

        static void Main(string[] args)
        {
            args = new string[] { "8000", "2725", "2725" };
            if (args.Length!=3)
            {
                Console.WriteLine("Usage: MapTubeExplorer {gridsize metres} {mapid1} {mapid2}");
                Console.WriteLine("Downloads two maps from the MapTube shapefile download web service with the map ids 1 and 2. Outputs a grid correlation based on the given grid size.");
            }
            else
            {
                //stage data and correlate
                float GridSize = Convert.ToSingle(args[0]);
                int MapIdX = Convert.ToInt32(args[1]);
                int MapIdY = Convert.ToInt32(args[2]);
                Console.WriteLine("MapTubeExplorer: GridSize=" + GridSize + " MapIdX=" + MapIdX + " MapIdY=" + MapIdY);
                GriddedData gdX = new GriddedData(MapIdX);
                GriddedData gdY = new GriddedData(MapIdY);
                Stopwatch timer = Stopwatch.StartNew();
                int CountX = gdX.Grid(GridSize);
                Console.WriteLine("Stopwatch=" + timer.ElapsedMilliseconds);
                Console.WriteLine("CountX=" + CountX + " grid cells");
                gdX.ExportCellShapefile("cell_mapid_2725.shp"); //DEBUG
                int CountY = gdY.Grid(GridSize);
                Console.WriteLine("CountY=" + CountY + " grid cells");
                double I = Correlation.SpatialBivariateMoranI(ref gdX.Cells, ref gdY.Cells);
                Console.WriteLine("I=" + I);
            }
        }
    }
}
