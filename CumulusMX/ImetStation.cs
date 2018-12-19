﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace CumulusMX
{
	internal class ImetStation : WeatherStation
	{
		private string sLineBreak = "\r\n";
		private bool midnightraindone;
		private double prevraintotal = -1;
		private int previousminute = 60;


		public ImetStation(Cumulus cumulus) : base(cumulus)
		{
			cumulus.Manufacturer = cumulus.INSTROMET;
			cumulus.LogMessage("ImetWaitTime="+cumulus.ImetWaitTime);
			cumulus.LogMessage("ImetBaudRate="+cumulus.ImetBaudRate);
			cumulus.LogMessage("Instromet: Attempting to open " + cumulus.ComportName);

			calculaterainrate = true;

			comport = new SerialPort(cumulus.ComportName, cumulus.ImetBaudRate, Parity.None, 8, StopBits.One) {Handshake = Handshake.None, RtsEnable = true, DtrEnable = true};

			try
			{
				comport.ReadTimeout = 1000;
				comport.Open();
				cumulus.LogMessage("COM port opened");
			}
			catch (Exception ex)
			{
				cumulus.LogMessage(ex.Message);
				//MessageBox.Show(ex.Message);
			}

			if (comport.IsOpen)
			{
				ImetSetLoggerInterval(cumulus.logints[cumulus.DataLogInterval]);
				if (cumulus.SyncTime)
				{
					SetStationClock();
				}

				// Read the data from the logger
				cumulus.CurrentActivity = "Reading archive data";
				startReadingHistoryData();
			}
		}

		private void ImetSetLoggerInterval(int interval)
		{
			cumulus.LogMessage("Setting logger interval to " + interval + " minutes");

			comport.Write("WRST,11," + interval*60 + sLineBreak);
			Thread.Sleep(cumulus.ImetWaitTime);
			// read the response
			string response = getResponse("wrst");

			string data = ExtractText(response, "wrst");
			cumulus.LogMessage("Response: " + data);
			cumulus.ImetLoggerInterval = interval;
		}

		private void SetStationClock()
		{
			string datestr = DateTime.Now.ToString("yyyyMMdd");
			string timestr = DateTime.Now.ToString("HHmmss");

			cumulus.LogMessage("WRTM," + datestr + ',' + timestr);

			comport.Write("WRTM," + datestr + ',' + timestr + sLineBreak);
			Thread.Sleep(cumulus.ImetWaitTime);
			// read the response
			string response = getResponse("wrtm");

			string data = ExtractText(response, "wrtm");
			cumulus.LogMessage("Response: " + data);
		}

		private string ReadStationClock()
		{
			comport.Write("RDTM" + sLineBreak);
			string response = getResponse("rdtm");
			string data = ExtractText(response, "rdtm");
			return data;
		}

		private void ProgressLogs()
		{
			// MainForm.LogMessage('Advance log pointer');
			// advance the pointer
			comport.Write("PRLG,1" + sLineBreak);
			Thread.Sleep(cumulus.ImetWaitTime);
			// read the response
			getResponse("prlg");
		}


		private void RegressLogs(DateTime ts) // Move the log pointer back until the archive record timestamp is earlier
			// than the supplied ts, or the logs cannot be regressed any further
		{
			const int TIMEPOS = 4;
			const int DATEPOS = 5;
			bool done = false;
			int numlogs = GetNumberOfLogs();
			int previousnumlogs = 0;
			bool dataOK;
			DateTime entryTS;

			cumulus.LogMessage("Regressing logs to before " + ts);
			// regress the pointer
			comport.Write("RGLG,1" + sLineBreak);
			// read the response
			string response = getResponse("rglg");
			do
			{
				List<string> sl = GetArchiveRecord();
				try
				{
					int hour = Convert.ToInt32(sl[TIMEPOS].Substring(0, 2));
					int minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
					int sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
					int day = Convert.ToInt32(sl[DATEPOS].Substring(0, 2));
					int month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
					int year = Convert.ToInt32(sl[DATEPOS].Substring(6, 2));
					cumulus.LogMessage("Logger entry : Y = " + year + ", M = " + month + ", D = " + day + ", H = " + hour + ", M = " + minute + ", S = " + sec);

					entryTS = new DateTime(year, month, day, hour, minute, sec, 0);
					dataOK = true;
				}
				catch (Exception Ex)
				{
					cumulus.LogMessage("Error in timestamp, unable to process logger data");
					dataOK = false;
					done = true;
					entryTS = DateTime.MinValue;
				}

				if (dataOK)
				{
					if (entryTS < ts)
					{
						done = true;
						cumulus.LogMessage("Regressed far enough");
					}
					else
					{
						// regress the pointer
						comport.Write("RGLG,1" + sLineBreak);
						// read the response
						response = getResponse("rglg");
						previousnumlogs = numlogs;
						numlogs = GetNumberOfLogs();
						cumulus.LogMessage("Number of logs = " + numlogs);
						if (numlogs == previousnumlogs)
						{
							done = true;
							cumulus.LogMessage("Cannot regress any further");
						}
					}
				}
			} while (!done);
		}

		private string getResponse(string expected)
		{
		  string response = "";

		  int attempts = 0;

		  try
		  {
			do
			{
			  attempts ++;
			  cumulus.LogDataMessage("Reading response from station, attempt "+attempts);
			  response = comport.ReadTo(sLineBreak);
			  cumulus.LogDataMessage("Response from station: "+response);
			  byte[] ba = Encoding.Default.GetBytes(response);
			  cumulus.LogDataMessage(BitConverter.ToString(ba));
			} while (!(response.Contains(expected))&&attempts<6);
		  }
		  catch (Exception e)
		  {
			// Probably a timeout, just exit
		  }

			return response;
		}

		private List<string> GetArchiveRecord()
		{
			List<string> sl = new List<string>();
			cumulus.LogMessage("Get next log - RDLG,1");
			// request the archive data
			comport.Write("RDLG,1" + sLineBreak);
			Thread.Sleep(cumulus.ImetWaitTime);
			// read the response
			string response = getResponse("rdlg");
			// extract the bit we want from all the other crap (echo, newlines, prompt etc)
			string data = ExtractText(response, "rdlg");
			cumulus.LogMessage(data);

			if (ValidChecksum(data))
			{
				try
				{
					// split the data
					sl = new List<string>(Regex.Split(data, ","));
				}
				catch
				{
				}
			}

			return sl;
		}

		private int GetNumberOfLogs()
		{
			int attempts = 0;
			int num = 0;
			bool valid = false;
			string data;
			do
			{
				attempts++;

				// read number of available archive entries
				comport.Write("LGCT" + sLineBreak);
				cumulus.LogMessage("Obtaining log count");
				Thread.Sleep(cumulus.ImetWaitTime);
				// read the response
				string response = getResponse("lgct");
				// extract the bit we want from all the other crap (echo, newlines, prompt etc)
				data = ExtractText(response, "lgct");
				cumulus.LogMessage("Response from LGCT=" + data);
				valid = ValidChecksum(data);
				if (valid)
				{
					cumulus.LogMessage("Checksum valid");
				}
				else
				{
					cumulus.LogMessage("!!! Checksum invalid !!!");
				}
			} while (!valid && (attempts < 3));

			if (valid)
			{
				num = 0;
				try
				{
					// split the data
					var st = new List<string>(Regex.Split(data, ","));

					if (st[1] != "")
					{
						num = Convert.ToInt32(st[1]);
					}
				}
				catch
				{
					num = 0;
				}
			}
			else
			{
				cumulus.LogMessage("Unable to read log count");
			}

			return num;
		}

		private bool ValidChecksum(string str)
		{
			try
			{
				// get length of string
				int strlen = str.Length;

				// split the data
				var sl = new List<string>(Regex.Split(str, ","));

				// get number of fields in string
				int len = sl.Count;
				// checksum is last field
				int csum = Convert.ToInt32((sl[len - 1]));

				// caclulate checksum of string
				uint sum = 0;
				int endpos = str.LastIndexOf(",");

				for (int i = 0; i <= endpos; i++)
				{
					sum = (sum + str[i])%256;
				}

				// 8-bit 1's complement
				sum = (~sum)%256;

				return (sum == csum);
			}
			catch
			{
				return false;
			}
		}

		private string ExtractText(string input, string after)
		{
			// return string after supplied string
			// used for extracting actual response from reply from station
			// assumes that the terminating CRLF is not present, as
			// readto() should have stripped this off
			int pos1 = input.IndexOf(after);
			//int pos2 = input.Length - 2;
			if (pos1>=0)
			{return input.Substring(pos1);}
			else
			{
			  return "";
			}
		}

		public override void startReadingHistoryData()
		{
			cumulus.LogMessage("Start reading history data");
			Console.WriteLine("Start reading history data...");
			//lastArchiveTimeUTC = getLastArchiveTime();

			LoadLastHoursFromDataLogs(cumulus.LastUpdateTime);

			bw = new BackgroundWorker();
			//histprog = new historyProgressWindow();
			//histprog.Owner = mainWindow;
			//histprog.Show();
			bw.DoWork += new DoWorkEventHandler(bw_DoWork);
			//bw.ProgressChanged += new ProgressChangedEventHandler(bw_ProgressChanged);
			bw.RunWorkerCompleted += new RunWorkerCompletedEventHandler(bw_RunWorkerCompleted);
			bw.WorkerReportsProgress = true;
			bw.RunWorkerAsync();
		}

		public override void Stop()
		{
		}

		private void bw_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
		{
			//histprog.histprogTB.Text = "Processed 100%";
			//histprog.histprogPB.Value = 100;
			//histprog.Close();
			//mainWindow.FillLastHourGraphData();

			cumulus.CurrentActivity = "Normal running";
			cumulus.LogMessage("Archive reading thread completed");
			DoDayResetIfNeeded();
			DoTrendValues(DateTime.Now);
			cumulus.StartTimers();
			StartLoop();
		}

		private void bw_DoWork(object sender, DoWorkEventArgs e)
		{
			getAndProcessHistoryData();
			// Do it again in case it took a long time and there are new entries
			getAndProcessHistoryData();
		}

		public override void getAndProcessHistoryData()
		{
			// Positions of fields in logger data
			//const int IDPOS = 1;
			//const int TYPEPOS = 2;
			const int INTERVALPOS = 3;
			const int TIMEPOS = 4;
			const int DATEPOS = 5;
			const int TEMP1MINPOS = 6;
			const int TEMP1MAXPOS = 7;
			const int TEMP1AVGPOS = 8;
			const int TEMP2MINPOS = 9;
			const int TEMP2MAXPOS = 10;
			const int TEMP2AVGPOS = 11;
			//const int RELHUMMINPOS = 12;
			//const int RELHUMMAXPOS = 13;
			const int RELHUMAVGPOS = 14;
			//const int PRESSMINPOS = 15;
			//const int PRESSMAXPOS = 16;
			const int PRESSAVGPOS = 17;
			//const int WINDMINPOS = 18;
			const int WINDMAXPOS = 19;
			const int WINDAVGPOS = 20;
			const int DIRPOS = 21;
			const int SUNPOS = 22;
			const int RAINPOS = 23;

			string response;
			bool rolloverdone;
			bool dataOK = false;
			DateTime timestamp = DateTime.MinValue;

			NumberFormatInfo provider = new NumberFormatInfo();
			provider.NumberDecimalSeparator = ".";

			DateTime startfrom = cumulus.LastUpdateTime;
			int startindex = 0;
			int year = startfrom.Year;
			int month = startfrom.Month;
			int day = startfrom.Day;
			int hour = startfrom.Hour;
			int minute = startfrom.Minute;
			int sec = startfrom.Second;


			cumulus.LogMessage("Last update time = " + hour + ":" + minute);

			int recordsdone = 0;

			if (FirstRun)
			{
				// First time Cumulus has run, "delete" all the log entries as there may be
				// vast numbers and they will take hours to download only to be discarded
				cumulus.LogMessage("First run: PRLG,32760");
				// regress the pointer
				comport.Write("PRLG,32760" + sLineBreak);
				// read the response
				response = getResponse("prlg");
			}


			cumulus.LogMessage("Downloading history from " + startfrom);
			Console.WriteLine("Reading archive data from " + startfrom + " - please wait");
			//RegressLogs(cumulus.LastUpdateTime);
			//bool valid = false;
			int numrecs = GetNumberOfLogs();
			cumulus.LogMessage("Logs available = " + numrecs);
			if (numrecs > 0)
			{
				cumulus.LogMessage("Number of history records = " + numrecs);
				// get the earliest record
				List<string> sl = GetArchiveRecord();
				try
				{
					hour = Convert.ToInt32(sl[TIMEPOS].Substring(0, 2));
					minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
					sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
					day = Convert.ToInt32(sl[DATEPOS].Substring(0, 2));
					month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
					year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
					cumulus.LogMessage("Logger entry : Y = " + year + ", M = " + month + ", D = " + day + ", H = " + hour + ", M = " + minute + ", S = " + sec);

					timestamp = new DateTime(year, month, day, hour, minute, sec, 0);
					dataOK = true;
				}
				catch
				{
					cumulus.LogMessage("Error in earliest timestamp, unable to process logger data");
					dataOK = false;
				}


				if (dataOK)
				{
					cumulus.LogMessage("Earliest timestamp " + timestamp);
					if (timestamp < cumulus.LastUpdateTime)
					{
						// startindex = 1;
						cumulus.LogMessage("-----Earliest timestamp is earlier than required");
						cumulus.LogMessage("-----Find first entry after " + cumulus.LastUpdateTime);
						startindex++; //  to allow for first log already read
						while ((startindex < numrecs) && (timestamp <= cumulus.LastUpdateTime))
						{
							// Move on to next entry
							ProgressLogs();
							sl = GetArchiveRecord();
							try
							{
								hour = Convert.ToInt32(sl[TIMEPOS].Substring(0, 2));
								minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
								sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
								day = Convert.ToInt32(sl[DATEPOS].Substring(0, 2));
								month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
								year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
								cumulus.LogMessage("Logger entry zero: Y = " + year + ", M = " + month + ", D = " + day + ", H = " + hour + ", M = " + minute + ", S = " + sec);

								timestamp = new DateTime(year, month, day, hour, minute, sec, 0);
								cumulus.LogMessage("New earliest timestamp " + timestamp);
							}
							catch (Exception E)

							{
								cumulus.LogMessage("Error in timestamp, skipping entry. Error = " + E.Message);
								timestamp = DateTime.MinValue;
							}

						  startindex++;
						}
					}
				}

				if (startindex < numrecs)
				{
					// We still have entries to process
					cumulus.LogMessage("-----Actual number of valid history records = " + (numrecs - startindex));
					// Compare earliest timestamp with the update time of the today file
					// and see if (they are on the same day
					int hourInc = cumulus.GetHourInc();


					// set up controls for end of day rollover
					int rollHour;
					if (cumulus.RolloverHour == 0)
					{
						rollHour = 0;
					}
					else if (cumulus.Use10amInSummer && (TimeZoneInfo.Local.IsDaylightSavingTime(DateTime.Now)))
					{
						// Locale is currently on Daylight time
						rollHour = cumulus.RolloverHour + 1;
					}

					else
					{
						// Locale is currently on Standard time or unknown
						rollHour = cumulus.RolloverHour;
					}

					// Check to see if (today"s rollover has been done
					// (we might be starting up in the rollover hour)

					int luhour = cumulus.LastUpdateTime.Hour;

					rolloverdone = luhour == rollHour;

					midnightraindone = luhour == 0;

					for (int i = startindex; i < numrecs; i++)
					{
						try
						{
							recordsdone++;
							sl = GetArchiveRecord();
							ProgressLogs();

							hour = Convert.ToInt32(sl[TIMEPOS].Substring(0, 2));
							minute = Convert.ToInt32(sl[TIMEPOS].Substring(3, 2));
							sec = Convert.ToInt32(sl[TIMEPOS].Substring(6, 2));
							day = Convert.ToInt32(sl[DATEPOS].Substring(0, 2));
							month = Convert.ToInt32(sl[DATEPOS].Substring(3, 2));
							year = Convert.ToInt32(sl[DATEPOS].Substring(6, 4));
							timestamp = new DateTime(year, month, day, hour, minute, sec);
							cumulus.LogMessage("Processing logger data entry " + i + " for " + timestamp);

							int interval = (int) (Convert.ToDouble(sl[INTERVALPOS], provider)/60);
							// Check for rollover

							if (hour != rollHour)
							{
								rolloverdone = false;
							}

							if (hour != 0)
							{
								midnightraindone = false;
							}

							if (sl[RELHUMAVGPOS] != "")
							{
								DoOutdoorHumidity((int) (Convert.ToDouble(sl[RELHUMAVGPOS], provider)), timestamp);
							}

							if ((sl[WINDAVGPOS] != "") && (sl[WINDMAXPOS] != "") && (sl[DIRPOS] != ""))
							{
								double windspeed = Convert.ToDouble(sl[WINDAVGPOS], provider);
								double windgust = Convert.ToDouble(sl[WINDMAXPOS], provider);
								int windbearing = Convert.ToInt32(sl[DIRPOS]);

								DoWind(windgust, windbearing, windspeed, timestamp);

								// add in "archivePeriod" minutes worth of wind speed to windrun
								WindRunToday += ((WindAverage*WindRunHourMult[cumulus.WindUnit]*interval)/60.0);

								DateTime windruncheckTS;
								if ((hour == rollHour) && (minute == 0))
									// this is the last logger entry before rollover
									// fudge the timestamp to make sure it falls in the previous day
								{
									windruncheckTS = timestamp.AddMinutes(-1);
								}
								else
								{
									windruncheckTS = timestamp;
								}

								CheckForWindrunHighLow(windruncheckTS);

								// update dominant wind bearing
								CalculateDominantWindBearing(Bearing, WindAverage, interval);
							}

							if (sl[TEMP1AVGPOS] != "")
							{
								DoOutdoorTemp(ConvertTempCToUser(Convert.ToDouble(sl[TEMP1AVGPOS], provider)), timestamp);

								// add in "archivePeriod" minutes worth of temperature to the temp samples
								tempsamplestoday = tempsamplestoday + interval;
								TempTotalToday = TempTotalToday + (OutdoorTemperature*interval);

								// update chill hours
								if (OutdoorTemperature < cumulus.ChillHourThreshold)
								{
									// add 1 minute to chill hours
									ChillHours = ChillHours + (interval/60);
								}

								// update heating/cooling degree days
								UpdateDegreeDays(interval);
							}

							if (sl[TEMP2AVGPOS] != "")
							{
								double temp2 = Convert.ToDouble(sl[TEMP2AVGPOS], provider);
								// supply in CELSIUS
								if (cumulus.LogExtraSensors)
								{
									DoExtraTemp(temp2, 1);
								}
								else
								{
									DoWetBulb(temp2, timestamp);
								}
							}

							if (sl[RAINPOS] != "")
							{
								var raintotal = Convert.ToDouble(sl[RAINPOS], provider);
								double raindiff;
								if (prevraintotal == -1)
								{
									raindiff = 0;
								}
								else
								{
									raindiff = raintotal - prevraintotal;
								}

								double rainrate = ConvertRainMMToUser((raindiff)*(60/cumulus.logints[cumulus.DataLogInterval]));

								DoRain(ConvertRainMMToUser(raintotal), rainrate, timestamp);

								prevraintotal = raintotal;
							}

							if ((sl[WINDAVGPOS] != "") && (sl[TEMP1AVGPOS] != ""))
							{
								// wind chill
								double tempinC = ConvertUserTempToC(OutdoorTemperature);
								double windinKPH = ConvertUserWindToKPH(WindAverage);
								double value = MeteoLib.WindChill(tempinC, windinKPH);
								// value is now in celsius, convert to units in use
								value = ConvertTempCToUser(value);
								DoWindChill(value, timestamp);
							}

							if (sl[PRESSAVGPOS] != "")
							{
								DoPressure(ConvertPressMBToUser(Convert.ToDouble(sl[PRESSAVGPOS], provider)), timestamp);
							}

							// Cause wind chill calc
							DoWindChill(0, timestamp);

							DoApparentTemp(timestamp);

							// sunshine hours
							if (sl[SUNPOS] != "")
							{
								DoSunHours(Convert.ToDouble(sl[SUNPOS], provider), timestamp);
							}

							cumulus.DoLogFile(timestamp, false);

							AddLastHourDataEntry(timestamp, Raincounter, OutdoorTemperature);
							RemoveOldLHData(timestamp);
							AddGraphDataEntry(timestamp, Raincounter, RainToday, RainRate, OutdoorTemperature, OutdoorDewpoint, ApparentTemperature, WindChill, HeatIndex,
								IndoorTemperature, Pressure, WindAverage, RecentMaxGust, AvgBearing, Bearing, OutdoorHumidity, IndoorHumidity, SolarRad, CurrentSolarMax, UV);
							RemoveOldGraphData(timestamp);
							AddLast3HourDataEntry(timestamp, Pressure, OutdoorTemperature);
							RemoveOldL3HData(timestamp);
							AddRecentDataEntry(timestamp, WindAverage, RecentMaxGust, WindLatest, Bearing, AvgBearing, OutdoorTemperature, WindChill, OutdoorDewpoint, HeatIndex,
								OutdoorHumidity, Pressure, RainToday, SolarRad, UV, Raincounter);
							DoTrendValues(timestamp);
							UpdatePressureTrendString();
							UpdateStatusPanel(timestamp);

							// Add current data to the lists of web service updates to be done
							cumulus.AddToWebServiceLists(timestamp);

							if ((hour == rollHour) && !rolloverdone)
							{
								// do rollover
								cumulus.LogMessage("Day rollover " + timestamp);
								DayReset(timestamp);

								rolloverdone = true;
							}

							if ((hour == 0) && !midnightraindone)
							{
								ResetMidnightRain(timestamp);
								ResetSunshineHours();
								midnightraindone = true;
							}
						}
						catch (Exception E)
						{
							cumulus.LogMessage("Error in data: " + E.Message);
						}
					}
				}
				else
				{
					cumulus.LogMessage("No history records to process");
				}
			}
			else
			{
				cumulus.LogMessage("No history records to process");
			}
		}


		public override void portDataReceived(object sender, SerialDataReceivedEventArgs e)
		{
		}

		public override void Start()
		{
			cumulus.LogMessage(DateTime.Now + "Starting Instromet data reading thread");

			try
			{
				while (true)
				{
					ImetGetData();
					if (cumulus.ImetLoggerInterval != cumulus.logints[cumulus.DataLogInterval])
					{
						// logging interval has changed; update station to match
						ImetSetLoggerInterval(cumulus.logints[cumulus.DataLogInterval]);
					}
					else
					{
						Thread.Sleep(2000);
					}
				}
			}
				// Catch the ThreadAbortException
			catch (ThreadAbortException)
			{
			}
			finally
			{
				comport.Close();
			}
		}

		private void ImetGetData()
		{
			const int TEMP1POS = 1;
			const int TEMP2POS = 2;
			const int RELHUMPOS = 3;
			const int PRESSPOS = 4;
			const int WINDPOS = 5;
			const int DIRPOS = 6;
			const int SUNPOS = 7;
			const int RAINPOS = 8;
			//const int CHECKSUMPOS = 9;

			DateTime now = DateTime.Now;

			int h = now.Hour;
			int min = now.Minute;

			if (min != previousminute)
			{
				previousminute = min;

				if (cumulus.SyncTime && (h == cumulus.ClockSettingHour) && (min == 0))
				{
					// It's 0400, set the station clock
					SetStationClock();
				}
			}

			comport.Write("RDLV" + sLineBreak);
			// read the response
			var response = getResponse("rdlv");

			if (ValidChecksum(response))
			{
				cumulus.LogDataMessage(response);
				// split the data
				var sl = new List<string>(Regex.Split(response, ","));

				// Parse data using decimal points rather than user's decimal separator
				NumberFormatInfo provider = new NumberFormatInfo();
				provider.NumberDecimalSeparator = ".";

				double temp1 = 0;
				double windspeed = 0;

				if (!string.IsNullOrEmpty(sl[TEMP1POS]))
				{
					temp1 = Convert.ToDouble(sl[TEMP1POS], provider);
					DoOutdoorTemp(ConvertTempCToUser(temp1), now);
				}

				if (!string.IsNullOrEmpty(sl[TEMP2POS]))
				{
					double temp2 = Convert.ToDouble(sl[TEMP2POS], provider);
					if (cumulus.LogExtraSensors)
					{
						// use second temp as Extra Temp 1
						DoExtraTemp(ConvertTempCToUser(temp2), 1);
					}
					else
					{
						// use second temp as wet bulb
						DoWetBulb(ConvertTempCToUser(temp2), now);
					}
				}

				if (!string.IsNullOrEmpty(sl[RELHUMPOS]))
				{
					double hum = Convert.ToDouble(sl[RELHUMPOS], provider);
					DoOutdoorHumidity((int) hum, now);
				}

				if (!string.IsNullOrEmpty(sl[PRESSPOS]))
				{
					double press = Convert.ToDouble(sl[PRESSPOS], provider);
					DoPressure(ConvertPressMBToUser(press), now);
				}

				if (!string.IsNullOrEmpty(sl[DIRPOS])&&!string.IsNullOrEmpty(sl[WINDPOS]))
				{
					int winddir = Convert.ToInt32(sl[DIRPOS], provider);
					windspeed = Convert.ToDouble(sl[WINDPOS], provider);

					DoWind(ConvertWindMSToUser(windspeed), winddir, ConvertWindMSToUser(windspeed), now);
				}
				if (!string.IsNullOrEmpty(sl[RAINPOS]))
				{
					double raintotal = Convert.ToDouble(sl[RAINPOS], provider);
					DoRain(ConvertRainMMToUser(raintotal), -1, now);
				}

				if (!string.IsNullOrEmpty(sl[SUNPOS]))
				{
					double sunhours = Convert.ToDouble(sl[SUNPOS], provider);
					DoSunHours(sunhours, now);
				}

				if (!string.IsNullOrEmpty(sl[TEMP1POS]))
				{
					double windchill = MeteoLib.WindChill(temp1, windspeed*3.6);
					DoWindChill(windchill, now);
				}

				DoApparentTemp(now);

				DoForecast("", false);

				UpdatePressureTrendString();

				UpdateStatusPanel(now);
			}
			else
			{
				cumulus.LogMessage("Invalid checksum:");
				cumulus.LogMessage(response);
			}

		    if (cumulus.ImetUpdateLogPointer)
		    {
                // Keep the log pointer current, to avoid large numbers of logs
                // being downloaded at next startup
		        ProgressLogs();
		    }
		}
	}
}