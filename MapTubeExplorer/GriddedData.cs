using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using GeoAPI.Geometries;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.ShapeFile;
using NetTopologySuite.Index.Quadtree;

namespace MapTubeExplorer
{
    /// <summary>
    /// One cell of gridded data bounded by the envelope with a data value
    /// </summary>
    class GridCell
    {
        public Envelope envelope;
        public float data;
        public GridCell(Envelope env,float data)
        {
            this.envelope = env;
            this.data = data;
        }
        /// <summary>
        /// Return an envelope match on grid cells A and B.
        /// </summary>
        /// <param name="A"></param>
        /// <param name="B"></param>
        /// <returns></returns>
        public static bool Match(GridCell A, GridCell B)
        {
            return A.envelope.Equals(B.envelope);
        }
    }

    /// <summary>
    /// A GriddedData is a wrapper for the data loaded from MapTube. It wraps an NTS FeatureCollection, providing
    /// the ability to instantiate from the MapTube web service and perform the gridding.
    /// NOTE: the gridding process is programmed to load an attribute called "data", which is what is returned from the MapTube file download service.
    /// This could be made configurable.
    /// </summary>
    class GriddedData
    {
        const string TempDir = "stageddata"; //TODO: app.config or command line? This can be changed to an absolute dir
        //public const string MapTubeWebRequest = "http://www.maptube.org/api/FileDownloadService.svc/{0}/{1}";
        public const string MapTubeWebRequest = "http://localhost:8080/api/FileDownloadService.svc/{0}/{1}";

        #region properties

        /// <summary>
        /// List of cells with bounding envelope and data value. This is the data used for the correlation.
        /// </summary>
        public List<GridCell> Cells;

        /// <summary>
        /// Internal data from the shapefile used to create the grid cells.
        /// </summary>
        protected Quadtree<Feature> QTFeatures;

        
        #endregion proterties

        /// <summary>
        /// Construct from a MapTube mapid.
        /// TODO: include method of obtaining the map id from the mobile API service
        /// </summary>
        /// <param name="MapId"></param>
        public GriddedData(int MapId)
        {
            if (!Directory.Exists(TempDir)) Directory.CreateDirectory(TempDir);

            QTFeatures = new Quadtree<Feature>();

            string ShpFilename = "mapid_" + MapId + ".shp";
            string DbfFilename = "mapid_" + MapId + ".dbf";

            //get the shapefile
            StageFile(string.Format(MapTubeWebRequest, MapId, "shp"),ShpFilename);
            //and the dbase attributes
            StageFile(string.Format(MapTubeWebRequest, MapId, "dbf"),DbfFilename);

            GeometryFactory gfac = new GeometryFactory();
            ShapefileDataReader shapeFileDataReader = new ShapefileDataReader(Path.Combine(TempDir,ShpFilename), gfac);
            ShapefileHeader shpHeader = shapeFileDataReader.ShapeHeader;
            DbaseFileHeader header = shapeFileDataReader.DbaseHeader;
            while (shapeFileDataReader.Read())
            {
                Feature feature = new Feature();
                AttributesTable attributesTable = new AttributesTable();
                string[] keys = new string[header.NumFields];
                IGeometry geometry = (Geometry)shapeFileDataReader.Geometry;
                for (int i = 0; i < header.NumFields; i++)
                {
                    DbaseFieldDescriptor fldDescriptor = header.Fields[i];
                    keys[i] = fldDescriptor.Name;
                    attributesTable.AddAttribute(fldDescriptor.Name, shapeFileDataReader.GetValue(i));
                }
                feature.Geometry = geometry;
                feature.Attributes = attributesTable;
                QTFeatures.Insert(geometry.EnvelopeInternal,feature); //add it to the quadtree index
            }
        }

        /// <summary>
        /// Load file (i.e. shapefile or dbf) to a local staging area.
        /// </summary>
        /// <param name="uri">Web request to get you the file</param>
        /// <param name="Filename">Filename to store the response stream. This is made to be relative to TempDir, so you can just pass in "mapid_1588.shp".</param>
        public bool StageFile(string uri,string Filename)
        {
            string FullPath = Path.Combine(TempDir, Filename);
            if (File.Exists(FullPath)) return true; //short circuit if file already in staging area - this is a big performace boost if a lot of files are being compared (removes the multiple http downloads)

            bool Success = File.Exists(FullPath);
            if (!Success)
            {
                Success = false;
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    using (FileStream fstream = File.Create(FullPath))
                    {
                        response.GetResponseStream().CopyTo(fstream);
                    }
                    Success = true;
                }
            }
            return Success;
        }

        /// <summary>
        /// Grid the currently loaded data and return the number of grid cells (sparse grid).
        /// </summary>
        /// <param name="size"></param>
        /// <returns></returns>
        public int Grid(float size)
        {
            Cells = new List<GridCell>();
            //find bounds of data from the feature collection already loaded
            Envelope env = new Envelope();
            foreach (Feature f in QTFeatures.QueryAll())
            {
                env.ExpandToInclude(f.Geometry.EnvelopeInternal);
            }
            float sizex = (float)Math.Ceiling(env.Width / size);
            float sizey = (float)Math.Ceiling(env.Height / size);
            Console.WriteLine("Grid: size="+size+" cells= " + sizex + " x " + sizey + "="+sizex*sizey );

            //find top left as lowest integer divisor of size
            double MinX = Math.Floor(env.MinX / size)*size;
            double Miny = Math.Floor(env.MinY / size)*size;
            double y = env.MinY;
            while (y<env.MaxY)
            {
                double x = env.MinX;
                while (x<env.MaxX)
                {
                    Envelope CellEnvelope = new Envelope(x, x + size, y, y + size);
                    GeometryFactory gfac = new GeometryFactory();
                    Geometry CellGeometry = (Geometry)gfac.ToGeometry(CellEnvelope); //need this for the intersect operation
                    double DataValue = 0;
                    bool IsValidCell = false; //flipped to true if there is any intersecting data on it i.e. it's not an empty cell
                    double CellArea = CellEnvelope.Area; //or size*size surely?
                    foreach (Feature f in QTFeatures.Query(CellEnvelope))
                    {
                        if (CellEnvelope.Intersects(f.Geometry.EnvelopeInternal)) //yes, this really can fail, so it's worth doing
                        {
                            //cut the geometry to the cell envelope and use the area left to pro-rata the data value
                            Geometry geom = (Geometry)f.Geometry;
                            Geometry IntersectGeom = (Geometry)geom.Intersection(CellGeometry);
                            double IntersectArea = IntersectGeom.Area;
                            if (IntersectArea > 0)
                            {
                                double Value = (double)f.Attributes["data"];
                                DataValue += Value * (IntersectArea / CellArea);
                                IsValidCell = true; //flag cell as valid and not empty
                            }
                        }
                    }
                    if (IsValidCell) Cells.Add(new GridCell(CellEnvelope, (float)DataValue));
                    x += size;
                }
                y += size;
            }

            //NOTE: after you've created all the Cells, you could delete the QTFeatures in order to save memory. This GriddedData object might be used
            //with multiple other GriddedData objects with the same grid size.

            return Cells.Count;
        }

        /// <summary>
        /// Write out a shapefile where all the features are square cells built from the current cell list with their associated data values.
        /// This allows for comparison between the original data and the gridded data.
        /// PRE: must have called Grid() first to make the cells.
        /// </summary>
        /// <param name="Filename">Full path and filename to output shapefile.</param>
        public void ExportCellShapefile(string OutFilename)
        {
            ShapefileDataWriter writer = new ShapefileDataWriter(OutFilename);
            DbaseFileHeader DBFHeader = new DbaseFileHeader(Encoding.UTF8);
            DBFHeader.AddColumn("data", 'N', 18, 8); //DoubleLength and DoubleDecimals from the shapefile spec
            DBFHeader.NumRecords = Cells.Count;
            writer.Header = DBFHeader;
            List<IFeature> ifs = new List<IFeature>();
            GeometryFactory gfac = new GeometryFactory();
            foreach (GridCell Cell in Cells)
            {
                Geometry CellGeometry = (Geometry)gfac.ToGeometry(Cell.envelope);
                AttributesTable Attributes = new AttributesTable();
                Attributes.AddAttribute("data", Cell.data);
                Feature f = new Feature(CellGeometry, Attributes);
                ifs.Add(f);
            }
            writer.Write(ifs);
        }
    }
}
