﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HISWebClient.BusinessObjects.Models;
using DotSpatial.Data;
using DotSpatial.Topology;
using System.Threading;
using HISWebClient.BusinessObjects;
using System.Configuration;

using log4net;

using HISWebClient.Util;

namespace HISWebClient.DataLayer
{
	  public abstract class SeriesSearcher
	{
		private int totalSeriesCount = 0;
		private int maxAllowedTimeseriesReturn = Convert.ToInt32(ConfigurationManager.AppSettings["maxAllowedTimeseriesReturn"].ToString()); //maximum ammount of number of timeseries that are returned

		private static readonly log4net.ILog logger
			= LogManager.GetLogger("QueryLog");


		public SearchResult GetSeriesCatalogInRectangle(Box extentBox, string[] keywords, double tileWidth, double tileHeight,
														DateTime startDate, DateTime endDate, WebServiceNode[] serviceIDs)//, BusinessObjects.Models.IProgressHandler bgWorker)
		{
			if (extentBox == null) throw new ArgumentNullException("extentBox");
			if (serviceIDs == null) throw new ArgumentNullException("serviceIDs");
			//if (bgWorker == null) throw new ArgumentNullException("bgWorker");

			if (keywords == null || keywords.Length == 0)
			{
				keywords = new[] { String.Empty };
			}

			//bgWorker.CheckForCancel();
			var extent = new Extent(extentBox.XMin, extentBox.YMin, extentBox.XMax, extentBox.YMax);
			var fullSeriesList = GetSeriesListForExtent(extent, keywords, tileWidth, tileHeight, startDate, endDate,
														serviceIDs, //bgWorker, 
														series => true);
			SearchResult resultFs = null;
			if (fullSeriesList.Count > 0)
			{
				//bgWorker.ReportMessage("Calculating Points...");
				resultFs = SearchHelper.ToFeatureSetsByDataSource(fullSeriesList);
			}
			
			//bgWorker.CheckForCancel();
			//var message = string.Format("{0} Series found.", totalSeriesCount);
			//bgWorker.ReportProgress(100, "Search Finished. " + message);
			return resultFs;
		}

		public List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> GetSeriesInRectangle(Box extentBox, string[] keywords, double tileWidth, double tileHeight,
													   DateTime startDate, DateTime endDate, WebServiceNode[] serviceIDs)//, BusinessObjects.Models.IProgressHandler bgWorker)
		{
			if (extentBox == null) throw new ArgumentNullException("extentBox");
			if (serviceIDs == null) throw new ArgumentNullException("serviceIDs");
			//if (bgWorker == null) throw new ArgumentNullException("bgWorker");

			if (keywords == null || keywords.Length == 0)
			{
				keywords = new[] { String.Empty };
			}

			//bgWorker.CheckForCancel();
			var extent = new Extent(extentBox.XMin, extentBox.YMin, extentBox.XMax, extentBox.YMax);
			var fullSeriesList = GetSeriesListForExtent(extent, keywords, tileWidth, tileHeight, startDate, endDate,
														serviceIDs, //bgWorker, 
														series => true);
			//SearchResult resultFs = null;
			//if (fullSeriesList.Count > 0)
			//{
			//    //bgWorker.ReportMessage("Calculating Points...");
			//    resultFs = SearchHelper.ToFeatureSetsByDataSource(fullSeriesList);
			//}

			//bgWorker.CheckForCancel();
			//var message = string.Format("{0} Series found.", totalSeriesCount);
			//bgWorker.ReportProgress(100, "Search Finished. " + message);
			List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> orderedList = fullSeriesList.OrderBy(x => x.ServCode).ToList();
			return orderedList;
		}
	  

		public SearchResult GetSeriesCatalogInPolygon(IList<IFeature> polygons, string[] keywords, double tileWidth, double tileHeight,
													  DateTime startDate, DateTime endDate, WebServiceNode[] serviceIDs, BusinessObjects.Models.IProgressHandler bgWorker)
		{
			if (polygons == null) throw new ArgumentNullException("polygons");
			if (bgWorker == null) throw new ArgumentNullException("bgWorker");
			if (polygons.Count == 0)
			{
				throw new ArgumentException("The number of polygons must be greater than zero.");
			}

			if (keywords == null || keywords.Length == 0)
			{
				keywords = new[] { String.Empty };
			}
			
			var fullSeriesList = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
			for (int index = 0; index < polygons.Count; index++)
			{
				if (polygons.Count > 1)
				{
					bgWorker.ReportMessage(string.Format("Processing polygons: {0} of {1}", index + 1, polygons.Count));
				}

				bgWorker.CheckForCancel();
				var polygon = polygons[index];
				var extentBox = new Extent(polygon.Envelope);
				var seriesForPolygon = GetSeriesListForExtent(extentBox, keywords, tileWidth, tileHeight, startDate,
															  endDate,
															  serviceIDs, //bgWorker,
															  item => polygon.Intersects(new Coordinate(item.Longitude, item.Latitude)));
				fullSeriesList.AddRange(seriesForPolygon);
			}

			SearchResult resultFs = null;
			if (fullSeriesList.Count > 0)
			{
				bgWorker.ReportMessage("Calculating Points...");
				resultFs = SearchHelper.ToFeatureSetsByDataSource(fullSeriesList);
			}
			
			bgWorker.CheckForCancel();
			var message = string.Format("{0} Series found.", totalSeriesCount);
			bgWorker.ReportProgress(100, "Search Finished. " + message);
			return resultFs;
		}

		private List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> GetSeriesListForExtent(Extent extent, IEnumerable<string> keywords, double tileWidth, double tileHeight,
													  DateTime startDate, DateTime endDate, ICollection<WebServiceNode> serviceIDs, 
													  //BusinessObjects.Models.IProgressHandler bgWorker, 
													  Func<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart, bool> seriesFilter)
		{
			var servicesToSearch = new List<Tuple<WebServiceNode[], Extent>>();
			if (serviceIDs.Count > 0)
			{
				foreach (var webService in serviceIDs)
				{
					if (webService.ServiceBoundingBox == null)
					{
						servicesToSearch.Add(new Tuple<WebServiceNode[], Extent>(new[] { webService }, extent));
						continue;
					}
					const double eps = 0.05; //tolerance (0.05 deg) used for services whose bounding box is one point
					var wsBox = webService.ServiceBoundingBox;
					var wsExtent = new Extent(wsBox.XMin - eps, wsBox.YMin - eps, wsBox.XMax + eps, wsBox.YMax + eps);
					if (wsExtent.Intersects(extent))
					{
						servicesToSearch.Add(new Tuple<WebServiceNode[], Extent>(new[] { webService }, wsExtent.Intersection(extent)));
					}
				}
			}
			else
			{
				servicesToSearch.Add(new Tuple<WebServiceNode[], Extent>(new WebServiceNode[] { }, extent));
			}

			var servicesWithExtents = new List<Tuple<WebServiceNode[], List<Extent>>>(servicesToSearch.Count);
			int totalTilesCount = 0;
			foreach (var wsInfo in servicesToSearch)
			{
				var tiles = SearchHelper.CreateTiles(wsInfo.Item2, tileWidth, tileHeight);
				servicesWithExtents.Add(new Tuple<WebServiceNode[], List<Extent>>(wsInfo.Item1, tiles));
				totalTilesCount += tiles.Count;
			}

			var fullSeriesList = new List<List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>>();
			long  currentTileIndex = 0;
			int tilesFinished = 0;
			totalSeriesCount = 0;

			//bgWorker.ReportProgress(0, "0 Series found");
			CancellationTokenSource cts = new CancellationTokenSource();

			var serviceLoopOptions = new ParallelOptions
				{
						//CancellationToken = bgWorker.CancellationToken,
#if (DEBUG)
						MaxDegreeOfParallelism = EnvironmentContext.LocalEnvironment() ? 1 : 2
#else
						MaxDegreeOfParallelism = 2 
#endif
				};
			var tileLoopOptions = new ParallelOptions
				{
						//CancellationToken = bgWorker.CancellationToken,
						CancellationToken = cts.Token,
						// Note: currently HIS Central returns timeout if many requests are sent in the same time.
						// To test set  MaxDegreeOfParallelism = -1
#if (DEBUG)
						MaxDegreeOfParallelism = EnvironmentContext.LocalEnvironment() ? 1 : 4
#else
						MaxDegreeOfParallelism = 4
#endif
				};
			try
			{
				Parallel.ForEach(servicesWithExtents, serviceLoopOptions, wsInfo =>
				{
					//bgWorker.CheckForCancel();
					var ids = wsInfo.Item1.Select(item => item.ServiceID).ToArray();
					var tiles = wsInfo.Item2;
					try
					{
						Parallel.ForEach(tiles, tileLoopOptions, tile =>
						{
							var current = Interlocked.Add(ref currentTileIndex, 1);
							//bgWorker.CheckForCancel();

							// Do the web service call
							var tileSeriesList = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();

							if (SearchSettings.AndSearch == true)
							{
								//CHANGES FOR "AND" SEARCH
								var totalTileSeriesList = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
								var tileSeriesList2 = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
								var tileSeriesList3 = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
								var tileSeriesList4 = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();

								SeriesComparer sc = new SeriesComparer();

								for (int i = 0; i < keywords.Count(); i++)
								{
									String keyword = keywords.ElementAt(i);

									string sampleMedium = String.Empty;
									string dataType = String.Empty;
									string valueType = string.Empty;

									var series = GetSeriesCatalogForBox(tile.MinX, tile.MaxX, tile.MinY, tile.MaxY, sampleMedium, dataType, valueType, keyword, startDate, endDate, ids, current, totalTilesCount);

									totalTileSeriesList.AddRange(series);
									if (tileSeriesList.Count() == 0)
									{
										if (i == 0)
										{
											tileSeriesList.AddRange(series);
										}
										else
										{
											break;
										}
									}
									else
									{

										tileSeriesList2.AddRange(tileSeriesList.Intersect(series, sc));
										tileSeriesList.Clear();
										tileSeriesList.AddRange(tileSeriesList2);
										tileSeriesList2.Clear();
									}
								}


								for (int i = 0; i < tileSeriesList.Count(); i++)
								{
									tileSeriesList4 = totalTileSeriesList.Where(item => (item.SiteName.Equals(tileSeriesList.ElementAt(i).SiteName))).ToList();
									tileSeriesList3.AddRange(tileSeriesList4);
								}

								tileSeriesList = tileSeriesList3;
							}
							else
							{
								tileSeriesList = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
								foreach (var keyword in keywords)
								{
									string sampleMedium = String.Empty;
									string dataType = String.Empty;
									string valueType = string.Empty;

									var series = GetSeriesCatalogForBox(tile.MinX, tile.MaxX, tile.MinY, tile.MaxY, sampleMedium, dataType, valueType, keyword, startDate, endDate, ids, current, totalTilesCount);

									tileSeriesList.AddRange(series);
								}
							}
							//END CHANGES FOR "AND" SEARCH

							//bgWorker.CheckForCancel();
							if (tileSeriesList.Count > 0)
							{
								var filtered = tileSeriesList.Where(seriesFilter).ToList();
								if (filtered.Count > 0)
								{
									lock (_lockGetSeries)
									{
										totalSeriesCount += filtered.Count;
										fullSeriesList.Add(filtered);
									}
								}
							}

							// Report progress
							var currentFinished = Interlocked.Add(ref tilesFinished, 1);
							if (totalSeriesCount > maxAllowedTimeseriesReturn)
							{
								//Maximum time series exceeded - register a delegate to add an exception to the aggregate exception returned by the task cancellation processing... 
								cts.Token.Register(() => {
									string errorMessage = String.Format("Search returned more than {0:#,###0} timeseries and was canceled. Please limit search area and/or Keywords.", maxAllowedTimeseriesReturn);
									InvalidOperationException exp = new InvalidOperationException(errorMessage);
									throw exp;
								});

								cts.Cancel();
							}
							var message = string.Format("{0} Series found", totalSeriesCount);
							var percentProgress = (currentFinished * 100) / totalTilesCount;
							//bgWorker.ReportProgress(percentProgress, message);


						});
					}
					catch (OperationCanceledException oex)
					{
						throw oex;
					}
				});
				// Collect all series into result list
				var result = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>(totalSeriesCount);
				fullSeriesList.ForEach(result.AddRange);
				return result;
			}
			catch (OperationCanceledException oex)
			{
				throw oex;
			}
		}

		private static readonly object _lockGetSeries = new object();

		/// <summary>
		/// Gets all data series within the geographic bounding box that match the
		/// specified criteria
		/// </summary>
		/// <param name="xMin">minimum x (longitude)</param>
		/// <param name="xMax">maximum x (longitude)</param>
		/// <param name="yMin">minimum y (latitude)</param>
		/// <param name="yMax">maximum y (latitude)</param>
		/// <param name="keyword">the concept keyword. If set to null,
		/// results will not be filtered by concept keyword</param>
		/// <param name="startDate">start date. If set to null, results will not be filtered by start date.</param>
		/// <param name="endDate">end date. If set to null, results will not be filtered by end date.</param>
		/// <param name="networkIDs">array of serviceIDs provided by GetServicesInBox.
		/// If set to null, results will not be filtered by web service.</param>
		/// <param name="bgWorker">Progress handler </param>
		/// <param name="currentTile">Current tile index </param>
		/// <param name="totalTilesCount">Total tiles number </param>
		/// <returns>A list of data series matching the specified criteria</returns>
		protected abstract IEnumerable<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> GetSeriesCatalogForBox(double xMin, double xMax, double yMin, double yMax, 
																			  string keyword, DateTime startDate, DateTime endDate,
																			  int[] networkIDs, 
																			  //BusinessObjects.Models.IProgressHandler bgWorker, 
																			  long currentTile, long totalTilesCount);

		protected abstract IEnumerable<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> GetSeriesCatalogForBox( double xMin, 
																														  double xMax, 
																														  double yMin, 
																													      double yMax,
																														  string sampleMedium,
																														  string dataType,
																														  string valueType,
																														  string keyword, 
																														  DateTime startDate, 
																														  DateTime endDate,
																														  int[] networkIDs,
																														  long currentTile, 
																														  long totalTilesCount);

	}
}

