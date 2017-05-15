using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Stonetip.Geo
{
    public class PbfListHelpers
    {
        /// <summary>
        ///     Helper to split and return list of lists based on interval n, e.g. groups of 3
        /// </summary>
        /// <typeparam name="T">generic type</typeparam>
        /// <param name="inputList">source list</param>
        /// <param name="groupSize">group size</param>
        /// <returns>list of lists&lt;T&gt; (will be empty if error)</returns>
        public static List<List<T>> SubdivideList<T>(List<T> inputList, int groupSize = 2)
        {
            try
            {
                var subdivisionsList = new List<List<T>>();

                for (var i = 0; i < inputList.Count; i += groupSize)
                    subdivisionsList.Add(inputList.GetRange(i, Math.Min(groupSize, inputList.Count - i)));

                return subdivisionsList;
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
                return new List<List<T>>();
            }
        }


        /// <summary>
        ///     Helper to get odd or even values from a list
        /// </summary>
        /// <param name="inputList">IEnumerable&lt;int&gt; list</param>
        /// <param name="odd">boolean indicating odd or even values</param>
        /// <returns>List of odd or even int values (will be empty if error)</returns>
        public static List<int> GetOddsOrEvensFromList(IEnumerable<int> inputList, bool odd)
        {
            try
            {
                return odd
                    ? inputList.Where((value, index) => index % 2 == 0).ToList()
                    : inputList.Where((value, index) => index % 2 != 0).ToList();
            }
            catch (Exception err)
            {
                Debug.WriteLine(err.Message);
                return new List<int>();
            }
        }
    }
}