﻿using HISWebClient.BusinessObjects.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace HISWebClient.DataLayer
{
	/// <summary>
	/// Search for data series using HIS Central 
	/// </summary>
	public class HISCentralSearcher : SeriesSearcher
	{
		#region Fields

		private readonly string _hisCentralUrl;
		private static readonly CultureInfo _invariantCulture = CultureInfo.InvariantCulture;

		#endregion

		#region Constructor

		/// <summary>
		/// Create a new HIS Central Searcher which connects to the HIS Central web
		/// services
		/// </summary>
		/// <param name="hisCentralUrl">The URL of HIS Central</param>
		public HISCentralSearcher(string hisCentralUrl)
		{
			hisCentralUrl = hisCentralUrl.Trim();
			if (hisCentralUrl.EndsWith("?WSDL", StringComparison.OrdinalIgnoreCase))
			{
				hisCentralUrl = hisCentralUrl.ToUpperInvariant().Replace("?WSDL", "");
			}
			_hisCentralUrl = hisCentralUrl;
		}

		#endregion

		#region Public methods

		public string GetWebServicesXml(string xmlFileName, int? codePage = null)
		{
			Encoding encoding = Encoding.ASCII;	//Default to ASCII encoding

			if ( null != codePage)
			{
				//Other than 'ASCII' encodings...
				Dictionary<int, Encoding> encodings = new Dictionary<int,Encoding>()
													  {
														  {Encoding.UTF7.CodePage, Encoding.UTF7},							//UTF-7
														  {Encoding.UTF8.CodePage, Encoding.UTF8},							//UTF-8
														  {Encoding.BigEndianUnicode.CodePage, Encoding.BigEndianUnicode},	//UTF-16
														  {Encoding.UTF32.CodePage, Encoding.UTF32},						//UTF-32
													  };

				foreach (int cp in encodings.Keys)
				{
					if (cp == codePage)
					{
						encoding = encodings[cp];
						break;
					}
				}
			}

			HttpWebResponse response = null;
			
			try
			{
				var url = _hisCentralUrl + "/GetWaterOneFlowServiceInfo";

				StringBuilder sb = new StringBuilder();

				byte[] buf = new byte[8192];

				//do get request
				HttpWebRequest request = (HttpWebRequest)
					WebRequest.Create(url);


				response = (HttpWebResponse)
					request.GetResponse();


				Stream resStream = response.GetResponseStream();

				string tempString = null;
				int count = 0;
				//read the data and print it
				do
				{
					count = resStream.Read(buf, 0, buf.Length);
					if (count != 0)
					{
						//tempString = Encoding.ASCII.GetString(buf, 0, count);
						tempString = encoding.GetString(buf, 0, count);

						sb.Append(tempString);
					}
				}
				while (count > 0);

				return sb.ToString();




				//var request = (HttpWebRequest)WebRequest.Create(url);
				////Endpoint is the URL to which u are making the request.
				//request.Method = "GET";
				//request.Credentials = CredentialCache.DefaultCredentials;
				//request.ContentType = "text/xml";

				//request.Timeout = 5000;

				//// send the request and get the response
				//response = (HttpWebResponse)request.GetResponse();

				//using (var responseStream = response.GetResponseStream())
				//{
				//    using (var localFileStream = new FileStream(xmlFileName, FileMode.OpenOrCreate))
				//    {
				//        var buffer = new byte[255];
				//        int bytesRead;
				//        while ((bytesRead = responseStream.Read(buffer, 0, buffer.Length)) > 0)
				//        {
				//            localFileStream.Write(buffer, 0, bytesRead);
				//        }
				//    }
				//}
				//Stream outputStream = response.GetResponseStream();
				//var objXMLReader = new XmlTextReader(response.GetResponseStream());
				//return outputStream;
			}
			finally
			{
				if (response != null)
				{
					response.Close();
				}
			}
		}

		public string GetOntologyTreeXML(string conceptKeyword)
		{
			HttpWebResponse response = null;
			WebClient myWebClient = new WebClient();
			
			try
			{
				var url = _hisCentralUrl + "/getOntologyTree";

				string data = "conceptKeyword=" + conceptKeyword;
				byte[] dataStream = Encoding.UTF8.GetBytes(data);

				StringBuilder sb = new StringBuilder();

				byte[] buf = new byte[8192];

				//do get request
				HttpWebRequest request = (HttpWebRequest)
					WebRequest.Create(url);

				request.Method = "POST";
				request.ContentType = "application/x-www-form-urlencoded";
				request.ContentLength = dataStream.Length;

				Stream newStream = request.GetRequestStream();

				// Send the data.
				newStream.Write(dataStream, 0, dataStream.Length);
				newStream.Close();

				response = (HttpWebResponse)
					request.GetResponse();


				Stream resStream = response.GetResponseStream();

				string tempString = null;
				int count = 0;
				//read the data and print it
				do
				{
					count = resStream.Read(buf, 0, buf.Length);
					if (count != 0)
					{
						tempString = Encoding.UTF8.GetString(buf, 0, count);

						sb.Append(tempString);
					}
				}
				while (count > 0);

				return sb.ToString();
		   
			}
			finally
			{
				if (response != null)
				{
					response.Close();
				}
			}
		}

		#endregion

		#region Private methods

		protected override IEnumerable<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> GetSeriesCatalogForBox(double xMin, double xMax, double yMin,
																			  double yMax, string keyword,
																			  DateTime startDate, DateTime endDate,
																			  int[] networkIDs,
																			  //IProgressHandler bgWorker, 
																			  long currentTile, long totalTilesCount)
		{
			var url = new StringBuilder();
			url.Append(_hisCentralUrl);
			url.Append("/GetSeriesCatalogForBox2");
			url.Append("?xmin=");
			url.Append(Uri.EscapeDataString(xMin.ToString(_invariantCulture)));
			url.Append("&xmax=");
			url.Append(Uri.EscapeDataString(xMax.ToString(_invariantCulture)));
			url.Append("&ymin=");
			url.Append(Uri.EscapeDataString(yMin.ToString(_invariantCulture)));
			url.Append("&ymax=");
			url.Append(Uri.EscapeDataString(yMax.ToString(_invariantCulture)));

			//to append the keyword
			url.Append("&conceptKeyword=");
			if (!String.IsNullOrEmpty(keyword))
			{
				url.Append(Uri.EscapeDataString(keyword));
			}

			//to append the list of networkIDs separated by comma
			url.Append("&networkIDs=");
			if (networkIDs != null)
			{
				var serviceParam = new StringBuilder();
				for (int i = 0; i < networkIDs.Length - 1; i++)
				{
					serviceParam.Append(networkIDs[i]);
					serviceParam.Append(",");
				}
				if (networkIDs.Length > 0)
				{
					serviceParam.Append(networkIDs[networkIDs.Length - 1]);
				}
				url.Append(Uri.EscapeDataString(serviceParam.ToString()));
			}

			//to append the start and end date
			url.Append("&beginDate=");
			url.Append(Uri.EscapeDataString(startDate.ToString("MM/dd/yyyy")));
			url.Append("&endDate=");
			url.Append(Uri.EscapeDataString(endDate.ToString("MM/dd/yyyy")));

			var keywordDesc = string.Format("[{0}. Tile {1}/{2}]",
											String.IsNullOrEmpty(keyword) ? "All" : keyword, currentTile,
											totalTilesCount);

			// Try to send request several times (in case, when server returns timeout)
			const int tryCount = 5;
			for (int i = 0; i < tryCount; i++)
			{
				try
				{
					//bgWorker.CheckForCancel();
					//bgWorker.ReportMessage(i == 0
					//                           ? string.Format("Sent request: {0}", keywordDesc)
					//                           : string.Format("Timeout has occurred for {0}. New Attempt ({1} of {2})...",
					//                               keywordDesc, i + 1, tryCount));

					var request = WebRequest.Create(url.ToString());
					request.Timeout = 30 * 1000;
					using (var response = request.GetResponse())
					using (var reader = XmlReader.Create(response.GetResponseStream()))
					{
						//bgWorker.ReportMessage(string.Format("Data received for {0}", keywordDesc));
						return ParseSeries(reader, startDate, endDate);
					}
				}
				catch (WebException ex)
				{
					if (ex.Status == WebExceptionStatus.Timeout)
					{
						//Timeout error - continue 
						continue;
					}
					
					throw;
				}
			}
			throw new WebException("Timeout. Please limit search area and/or Keywords.", WebExceptionStatus.Timeout);
		}

		private IEnumerable<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart> ParseSeries(XmlReader reader, DateTime startDate, DateTime endDate)
		{
			var seriesList = new List<BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart>();
			while (reader.Read())
			{
				if (reader.NodeType == XmlNodeType.Element)
				{
					if (reader.Name == "SeriesRecord")
					{
						//Read the site information
						var series = ReadSeriesFromHISCentral(reader);
						if (series != null)
						{
							//BCC - 14-Jul-2015 - Revise series date range checking...
							// Update BeginDate/EndDate/ValueCount to the user-specified range
							//SearchHelper.UpdateDataCartToDateInterval(series, startDate, endDate);
							//seriesList.Add(series);
							//
							//BCC - 23-Jul-2015 - Comment out changes for now...
							//if ( SearchHelper.IsSeriesInDateRange(series, startDate, endDate))
							//{
								SearchHelper.UpdateDataCartToDateInterval(series, startDate, endDate);
								seriesList.Add(series);
							//}
						}
					}
				}
			}
			return seriesList;
		}

		/// <summary>
		/// Read the list of series from the XML that is returned by HIS Central
		/// </summary>
		/// <param name="reader">the xml reader</param>
		/// <returns>the list of intermediate 'SeriesDataCart' objects</returns>
		private BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart ReadSeriesFromHISCentral(XmlReader reader)
		{
			var series = new BusinessObjects.Models.SeriesDataCartModel.SeriesDataCart();
			while (reader.Read())
			{
				var nodeName = reader.Name.ToLower();
				if (reader.NodeType == XmlNodeType.Element)
				{
					switch (nodeName)
					{
						case "servcode":
							reader.Read();
							series.ServCode = reader.Value;
							break;
						case "servurl":
							reader.Read();
							series.ServURL = reader.Value;
							break;
						case "location":
							reader.Read();
							series.SiteCode = reader.Value;
							break;
						case "varcode":
							reader.Read();
							series.VariableCode = reader.Value;
							break;
						case "varname":
							reader.Read();
							series.VariableName = reader.Value;
							break;
						case "begindate":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.BeginDate = Convert.ToDateTime(reader.Value, _invariantCulture);
							else
								return null;
							break;
						case "enddate":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.EndDate = Convert.ToDateTime(reader.Value, _invariantCulture);
							else
								return null;
							break;
						case "valuecount":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.ValueCount = Convert.ToInt32(reader.Value);
							else
								return null;
							break;
						case "sitename":
							reader.Read();
							series.SiteName = reader.Value;
							break;
						case "latitude":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.Latitude = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
							else
								return null;
							break;
						case "longitude":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.Longitude = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
							else
								return null;
							break;
						case "datatype":
							reader.Read();
							series.DataType = reader.Value;
							break;
						case "valuetype":
							reader.Read();
							series.ValueType = reader.Value;
							break;
						case "samplemedium":
							reader.Read();
							series.SampleMedium = reader.Value;
							break;
						case "timeunits":
							reader.Read();
							series.TimeUnit = reader.Value;
							break;
						case "conceptkeyword":
							reader.Read();
							series.ConceptKeyword = reader.Value;
							break;
						case "gencategory":
							reader.Read();
							series.GeneralCategory = reader.Value;
							break;
						case "timesupport":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.TimeSupport = Convert.ToDouble(reader.Value, CultureInfo.InvariantCulture);
							break;
						case "isregular":
							reader.Read();
							if (!String.IsNullOrWhiteSpace(reader.Value))
								series.IsRegular = Convert.ToBoolean(reader.Value);
							break;
						case "variableunits":
							reader.Read();
							series.VariableUnits = reader.Value;
							break;
					}
				}
				else if (reader.NodeType == XmlNodeType.EndElement && nodeName == "seriesrecord")
				{
					return series;
				}
			}

			return null;
		}

		#endregion
	}
}