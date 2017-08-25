using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HyperSpectralWPF
{
    /// <summary>
    /// Data structure used for storing H5 file image data.
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// Attributes
        /// </summary>
        private HDFqlCursor myCursor     = null;
        private string      fileName     = null;
        private float[,,]   data         = null;
        private float       maxValue     = 0.0F;
        private float       minValue     = 255.0F;
        private int         imageWidth   = 0;
        private int         imageHeight  = 0;
        private int         lambdaCount  = 0;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="file"></param>
        public ImageData(string file)
        {
            Initialize(file);
        }

        /// <summary>
        /// Retrieves all of the image data from the .h5 file and
        /// initializes all of the data attributes.
        /// </summary>
        /// <param name="file">The .h5 file specified by the user</param>
        private void Initialize(string file)
        {
            // Get the relative path of the file since only relative paths work as of right now.
            // TODO: Make absolute paths work.
            string relativePath = GetRelativePath(file, System.IO.Directory.GetCurrentDirectory());

            fileName = file;

            // Open the .h5 file specified by the user
            HDFql.Execute("USE FILE " + relativePath);

            // Create myCursor "myCursor" and use it
            myCursor = new HDFqlCursor();
            HDFql.CursorUse(myCursor);

            string path = "";

            HDFql.Execute("SHOW DATASET LIKE {50e35494-f4dd-4122-96f8-4d47c927abe5}/resultarray/inputdata WHERE DATATYPE IS INT");
            if (HDFql.CursorNext() == HDFql.Success)
            {
                path = HDFql.CursorGetChar();
            }
            else
            {
                HDFql.Execute("SHOW DATASET LIKE inputdata WHERE DATATYPE IS INT");

                if (HDFql.CursorNext() == HDFql.Success)
                {
                    path = HDFql.CursorGetChar();
                }
                else
                {
                    HDFql.Execute("SHOW DATASET LIKE Cube/resultarray/inputdata WHERE DATATYPE IS INT");

                    if (HDFql.CursorNext() == HDFql.Success)
                    {
                        path = HDFql.CursorGetChar();
                    }
                }
            }

            // Populate cursor "myCursor" with size of dataset "example_dataset" and display it
            if (path == "{50e35494-f4dd-4122-96f8-4d47c927abe5}/resultarray/inputdata")
            {
                HDFql.Execute("USE GROUP {50e35494-f4dd-4122-96f8-4d47c927abe5}");
                HDFql.Execute("USE GROUP resultarray");
                HDFql.Execute("USE DATASET inputdata");
            }
            else if (path == "inputdata")
            {
                HDFql.Execute("USE DATASET inputdata");
            }
            else
            {
                HDFql.Execute("USE GROUP Cube");
                HDFql.Execute("USE GROUP resultarray");
                HDFql.Execute("USE DATASET inputdata");
            }

            HDFql.Execute("SHOW SIZE inputdata");
            HDFql.CursorFirst();
            Console.WriteLine("Dataset size: {0}", HDFql.CursorGetInt());

            HDFql.Execute("SHOW DIMENSION inputdata");
            HDFql.CursorFirst();
            Console.WriteLine("Dataset size: {0}", HDFql.CursorGetInt());

            // Console.WriteLine lambda count, should be 78
            HDFql.CursorFirst(null);
            lambdaCount = HDFql.CursorGetInt() != null ? (int)HDFql.CursorGetInt() : 0;
            Console.WriteLine("inputdata dimension 0 (Λ): " + lambdaCount);

            // Console.WriteLine the the x and y dimensions of the dataset
            HDFql.CursorAbsolute(null, 2);
            int xDimension = HDFql.CursorGetInt() != null ? (int)HDFql.CursorGetInt() : 0;
            Console.WriteLine("inputdata dimension 2 (X): " + xDimension);

            HDFql.CursorAbsolute(null, 3);
            int yDimension = HDFql.CursorGetInt() != null ? (int)HDFql.CursorGetInt() : 0;
            Console.WriteLine("inputdata dimension 3 (Y): " + yDimension);

            // Set the size of the data array to be lambdaCount * xDimension * yDimension
            data = new float[lambdaCount, xDimension, yDimension];

            // Register variable "data" for subsequent use (by HDFql)
            HDFql.VariableRegister(data);

            // Select (y.e. read) dataset into variable "data"
            HDFql.Execute("SELECT FROM inputdata INTO MEMORY " + HDFql.VariableGetNumber(data));
            
            // Unregister variable "data" as it is no longer used/needed (by HDFql)
            HDFql.VariableUnregister(data);

            // Set the length and width of the textures
            imageWidth  = yDimension;
            imageHeight = xDimension;

            FindMinAndMaxValue();
        }

        /// <summary>
        /// Finds the min and max values in the image data.
        /// </summary>
        private void FindMinAndMaxValue()
        {
            // Retrieve the maximum value
            for (int lambda = 0; lambda < lambdaCount; lambda++)
            {
                for (int y = 0; y < imageHeight; y++)
                {
                    for (int x = 0; x < imageWidth; x++)
                    {
                        if (data[lambda, y, x] > maxValue)
                        {
                            maxValue = data[lambda, y, x];
                        }

                        if (data[lambda, y, x] < minValue)
                        {
                            minValue = data[lambda, y, x];
                        }
                    }
                }
            }

            Console.WriteLine(maxValue);
            Console.WriteLine(minValue);
        }
        
        /// <summary>
        /// Transforms an absolute path into a relative path.
        /// </summary>
        /// <param name="filePath">The path to transform.</param>
        /// <param name="folder">The current working directory</param>
        /// <returns>The relative path generated by the method</returns>
        private static string GetRelativePath(string filePath, string folder)
        {
            Uri pathUri = new Uri(filePath);

            // Folders must end in a slash
            if (!folder.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString()))
            {
                folder += System.IO.Path.DirectorySeparatorChar;
            }

            Uri folderUri = new Uri(folder);

            return Uri.UnescapeDataString(folderUri.MakeRelativeUri(pathUri).ToString().Replace('/', System.IO.Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Returns the file name that is associated with this data set.
        /// </summary>
        /// <returns>The file name that is associated with this data.</returns>
        public string GetFileName()
        {
            return fileName;
        }

        /// <summary>
        /// Returns the data set.
        /// </summary>
        /// <returns>The data set</returns>
        public float[,,] GetData()
        {
            return data;
        }

        /// <summary>
        /// Return the image width
        /// </summary>
        /// <returns>The image width</returns>
        public int GetWidth()
        {
            return imageWidth;
        }

        /// <summary>
        /// Returns the image height
        /// </summary>
        /// <returns>The image height</returns>
        public int GetHeight()
        {
            return imageHeight;
        }

        /// <summary>
        /// Returns the lambda count
        /// </summary>
        /// <returns>The lambda count</returns>
        public int GetLambdaCount()
        {
            return lambdaCount;
        }

        /// <summary>
        /// Returns the maximum value in the data set.
        /// </summary>
        /// <returns>The maximum value in the data set</returns>
        public float GetMaximum()
        {
            return maxValue;
        }

        /// <summary>
        /// Returns the minimum value in the data set.
        /// </summary>
        /// <returns>The minimum value in the data set</returns>
        public float GetMinimum()
        {
            return minValue;
        }
    }
}
