﻿using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using ServiceStack;


namespace CumulusMX
{
	internal sealed class EcowittLocalApi(Cumulus cumul) : IDisposable
	{
		private readonly Cumulus cumulus = cumul;
		private static readonly NumberFormatInfo invNum = CultureInfo.InvariantCulture.NumberFormat;

		public LiveData GetLiveData(CancellationToken token)
		{
			// http://ip-address/get_livedata_info
			//
			// Returns an almighty mess! They couldn't have made this any worse if they tried!
			// All values are returned as strings - including integers and decimals
			// Some values include the units in the value string, others have a separate field for the unit
			// The separate sensors return an arrays that only ever contain a single object
			//
			//	{
			//		"common_list": [
			//			{
			//				id: "ITEM_ID",
			//				val: "VALUE[ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "VALUE"]
			//			},
			//			{etc}
			//		],
			//		"rain": [
			//			{
			//				id: "ITEM_ID",
			//				val: "VALUE[ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "VALUE"]
			//			},
			//			{etc}
			//		],
			//		"piezoRain": [
			//			{
			//				id: "ITEM_ID",
			//				val: "[DECIMAL][ [UNIT]]",
			//				unit: "UNIT",
			//				[Battery: "[INT]"
			//			},
			//			{etc}
			//		],
			//		"wh25": [
			//			{
			//				"intemp": "[DECIMAL]",
			//				"unit": "C|F",
			//				"inhumi": "[INT]%",
			//				"abs": "[DECIMAL] [hPa|???]",
			//				"rel": "[DECIMAL] [hPa|???]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"lightning": [
			//			{
			//				"distance": "[INT] [km|???]",
			//				"timestamp": "MM/DD/YYYY HH:MM:SS",
			//				"count": "[INT]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"co2": [
			//			{
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"humidity": "[INT]%",
			//				"PM25": "[DECIMAL]",
			//				"PM25_RealAQI": "[INT]",
			//				"PM25_24HAQI": "[INT]",
			//				"PM10": "[DECIMAL]",
			//				"PM10_RealAQI": "[INT]",
			//				"PM10_24HAQI": "[INT]",
			//				"CO2": "[INT]",
			//				"CO2_24H": "[INT]",
			//				"battery": "[INT]"
			//			}
			//		],
			//		"ch_pm25": [
			//			{
			//				"channel": "[1-4]",
			//				"PM25": "[DECIMAL]",
			//				"PM25_RealAQI": "[INT]",
			//				"PM25_24HAQI": "[INT]",
			//				"battery": "[INT]"
			//			},
			//			{etc}
			//		],
			//		"ch_aisle": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"battery": "[INT]",
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"humidity": "[[INT]%|None]"
			//			},
			//			{etc}
			//		],
			//		"ch_soil": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"battery": "[INT",
			//				"humidity": "[INT]%"
			//			},
			//			{etc}
			//		],
			//		"ch_temp": [
			//			{
			//				"channel": "[1-16]",
			//				"name": "",
			//				"temp": "[DECIMAL]",
			//				"unit": "[C|F]",
			//				"battery": "[INT]"
			//			},
			//			{etc}
			//		],
			//		"ch_leaf": [
			//			{
			//				"channel": "[1-??]",
			//				"name": "",
			//				"humidity": "[INT]%"
			//				"battery": "[INT]",
			//			},
			//			{etc}
			//		]
			//	}
			//
			//
			// Sample:
			// {"common_list": [{"id": "0x02", "val": "23.5", "unit": "C"}, {"id": "0x07", "val": "57%"}, {"id": "3", "val": "23.5", "unit": "C"}, {"id": "0x03", "val": "14.5", "unit": "C"}, {"id": "0x0B", "val": "9.00 km/h"}, {"id": "0x0C", "val": "9.00 km/h"}, {"id": "0x19", "val": "26.64 km/h"}, {"id": "0x15", "val": "646.57 W/m2"}, {"id": "0x17", "val": "3"}, {"id": "0x0A", "val": "295"}], "rain": [{"id": "0x0D", "val": "0.0 mm"}, {"id": "0x0E", "val": "0.0 mm/Hr"}, {"id": "0x10", "val": "0.0 mm"}, {"id": "0x11", "val": "5.0 mm"}, {"id": "0x12", "val": "27.1 mm"}, {"id": "0x13", "val": "681.4 mm", "battery": "0"}], "piezoRain": [{"id": "0x0D", "val": "0.0 mm"}, {"id": "0x0E", "val": "0.0 mm/Hr"}, {"id": "0x10", "val": "0.0 mm"}, {"id": "0x11", "val": "10.7 mm"}, {"id": "0x12", "val": "32.3 mm"}, {"id": "0x13", "val": "678.3 mm", "battery": "5"}], "wh25": [{"intemp": "26.0", "unit": "C", "inhumi": "56%", "abs": "993.0 hPa", "rel": "1027.4 hPa", "battery": "0"}], "lightning": [{"distance": "12 km", "timestamp": "07/15/2024 20: 46: 42", "count": "0", "battery": "3"}], "co2": [{"temp": "24.4", "unit": "C", "humidity": "62%", "PM25": "0.9", "PM25_RealAQI": "4", "PM25_24HAQI": "7", "PM10": "0.9", "PM10_RealAQI": "1", "PM10_24HAQI": "2", "CO2": "323", "CO2_24H": "348", "battery": "6"}], "ch_pm25": [{"channel": "1", "PM25": "6.0", "PM25_RealAQI": "25", "PM25_24HAQI": "24", "battery": "5"}, {"channel": "2", "PM25": "8.0", "PM25_RealAQI": "33", "PM25_24HAQI": "32", "battery": "5"}], "ch_leak": [{"channel": "2", "name": "", "battery": "4", "status": "Normal"}], "ch_aisle": [{"channel": "1", "name": "", "battery": "0", "temp": "24.9", "unit": "C", "humidity": "61%"}, {"channel": "2", "name": "", "battery": "0", "temp": "25.7", "unit": "C", "humidity": "64%"}, {"channel": "3", "name": "", "battery": "0", "temp": "23.6", "unit": "C", "humidity": "63%"}, {"channel": "4", "name": "", "battery": "0", "temp": "34.9", "unit": "C", "humidity": "83%"}, {"channel": "5", "name": "", "battery": "0", "temp": "-14.4", "unit": "C", "humidity": "None"}, {"channel": "6", "name": "", "battery": "0", "temp": "31.5", "unit": "C", "humidity": "56%"}, {"channel": "7", "name": "", "battery": "0", "temp": "8.2", "unit": "C", "humidity": "50%"}], "ch_soil": [{"channel": "1", "name": "", "battery": "5", "humidity": "56%"}, {"channel": "2", "name": "", "battery": "4", "humidity": "47%"}, {"channel": "3", "name": "", "battery": "5", "humidity": "27%"}, {"channel": "4", "name": "", "battery": "5", "humidity": "50%"}, {"channel": "5", "name": "", "battery": "4", "humidity": "54%"}, {"channel": "6", "name": "", "battery": "4", "humidity": "47%"}], "ch_temp": [{"channel": "1", "name": "", "temp": "21.5", "unit": "C", "battery": "3"}, {"channel": "2", "name": "", "temp": "16.4", "unit": "C", "battery": "5"}], "ch_leaf": [{"channel": "1", "name": "CH1 Leaf Wetness", "humidity": "10%", "battery": "5"}]}

			string responseBody;
			int responseCode;
			int retries = 2;

			int retry = 1;

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("GetLiveData: Invalid station IP address: " + cumulus.Gw1000IpAddress);
				return null;
			}


			do
			{
				try
				{
					var url = $"http://{cumulus.Gw1000IpAddress}/get_livedata_info";

					// we want to do this synchronously, so .Result
					using (var response = cumulus.MyHttpClient.GetAsync(url, token).Result)
					{
						responseBody = response.Content.ReadAsStringAsync(token).Result;
						responseCode = (int) response.StatusCode;
						cumulus.LogDebugMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response code: {responseCode}");
						cumulus.LogDataMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Response: {responseBody}");
					}

					if (responseCode != 200)
					{
						cumulus.LogWarningMessage($"LocalApi.GetLiveData: Ecowitt Local API GetLiveData Error: {responseCode}");
						Cumulus.LogConsoleMessage($" - Error {responseCode}", ConsoleColor.Red);
						return null;
					}


					if (responseBody == "{}")
					{
						cumulus.LogMessage("LocalApi.GetLiveData: Ecowitt Local API GetLiveData: No data was returned.");
						Cumulus.LogConsoleMessage(" - No Live data available");
						return null;
					}
					else if (responseBody.StartsWith('{')) // sanity check
					{
						// Convert JSON string to an object
						LiveData json = responseBody.FromJson<LiveData>();
						return json;
					}
				}
				catch (System.Net.Http.HttpRequestException ex)
				{
					if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
					{
						cumulus.LogErrorMessage("GetLiveData: Error - This Station does not support the HTTP API!");
					}
					else
					{
						cumulus.LogExceptionMessage(ex, "GetLiveData: HTTP Error");
					}
				}
				catch (Exception ex)
				{
					cumulus.LogExceptionMessage(ex, "GetLiveData: Error");
				}
			} while (retries-- > 0);

			return null;
		}


		public async Task<SensorInfo[]> GetSensorInfo(CancellationToken token)
		{
			// http://ip-address/get_sensors_info?page=1
			// http://ip-address/get_sensors_info?page=2

			if (!Utils.ValidateIPv4(cumulus.Gw1000IpAddress))
			{
				cumulus.LogErrorMessage("GetSensorInfo: Invalid station IP address: " +  cumulus.Gw1000IpAddress);
				return null;
			}

			SensorInfo[] sensors1 = [];
			SensorInfo[] sensors2 = [];

			try
			{
				var url1 = $"http://{cumulus.Gw1000IpAddress}/get_sensors_info?page=1";
				var url2 = $"http://{cumulus.Gw1000IpAddress}/get_sensors_info?page=2";


				var task1 = cumulus.MyHttpClient.GetStringAsync(url1, token);
				var task2 = cumulus.MyHttpClient.GetStringAsync(url2, token);

				// Wait for both tasks to complete
				await Task.WhenAll(task1, task2);

				// Retrieve the results
				string result1 = await task1;
				string result2 = await task2;

				cumulus.LogDataMessage("GetSensorInfo: Page 1 = " + result1);
				cumulus.LogDataMessage("GetSensorInfo: Page 2 = " + result2);

				if (!string.IsNullOrEmpty(result1))
				{
					sensors1 = result1.FromJson<SensorInfo[]>();
				}
				if (!string.IsNullOrEmpty(result2))
				{
					sensors2 = result2.FromJson<SensorInfo[]>();
				}

				var retArr = new SensorInfo[sensors1.Length + sensors2.Length];
				sensors1.CopyTo(retArr, 0);
				sensors2.CopyTo(retArr, sensors1.Length);

				return retArr;
			}
			catch (System.Net.Http.HttpRequestException ex)
			{
				if (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
				{
					cumulus.LogErrorMessage("GetSensorInfo: Error - This Station does not support the HTTP API!");
				}
				else
				{
					cumulus.LogExceptionMessage(ex, "GetSensorInfo: HTTP Error");
				}
			}
			catch (Exception ex)
			{
				cumulus.LogExceptionMessage(ex, "GetSensorInfo: Error");
			}

			return null;
		}


		public void GetVersion(CancellationToken token)
		{
			// http://ip-address/get_version

			// response
			//	{
			//		"version":	"Version: GW1100A_V2.3.4",
			//		"newVersion":	"0",
			//		"platform":	"ecowitt"
			//	}

		}

		public void GetDeviceInfo(CancellationToken token) 
		{
			// http://ip-address/get_device_info

			//{
			//	"sensorType":	"1",
			//	"rf_freq":	"1",
			//	"AFC":	"0",
			//	"tz_auto":	"1",
			//	"tz_name":	"",
			//	"tz_index":	"39",
			//	"dst_stat":	"1",
			//	"radcompensation":	"0",
			//	"date":	"2024-09-06T16:36",
			//	"upgrade":	"0",
			//	"apAuto":	"1",
			//	"newVersion":	"0",
			//	"curr_msg":	"Current version:V2.3.4\r\n- Optimize RF reception performance.\r\n- Fix the issue of incorrect voltage upload for wh34/wh35/wh68 batteries.",
			//	"apName":	"GW1100A-WIFID4D3",
			//	"APpwd":	"",
			//	"time":	"20"
			//}
		}

		public void SetDeviceInfo(CancellationToken token)
		{
			// http://ip-address/set_device_info

			// POST

		}

		public void GetUnits(CancellationToken token)
		{
			// http://ip-address/get_units_info
			
			// response
			//{
			//	"temperature": "0",      0=C 1=F
			//	"pressure": "0",         0=hPa 1=inHg 2=mmHg
			//	"wind": "2",             0=ms 1=km/h 2=mph 3=knots
			//	"rain": "0",             0=mm 1=in
			//	"light": "1"             0=kLux=? 1=W/m2 2=kfc
			//}
		}

		public void SetUnits(CancellationToken token)
		{
			// http://ip-address/set_units_info

			// POST
			//{temperature: "1", pressure: "0", wind: "2", rain: "0", light: "1"}

			// response = 200 - OK
		}


		public void SetLogin(string password)
		{
			// http://ip-address/set_login_info

			// POST
			//{
			//	"pwd":""
			//}

			// Response
			//{
			//	"status":	"1",
			//	"online":	"0",
			//	"msg":	"success"
			//}
		}

		public void GetRainTotals(CancellationToken token)
		{
			// http://ip-address/get_rain_totals

			// response
			//{
			//	"rainFallPriority": "1",       0=No Guage 1=Traditional 2=Piezo
			//	"list":	[
			//		{
			//			"gauge": "No rain gauge",
			//			"value": "0"
			//		}, {
			//			"gauge": "Traditional rain gauge",
			//			"value": "1"
			//		}, {
			//			"gauge": "Piezoelectric rain gauge",
			//			"value": "2"
			//		}
			//	],
			//	"rainDay": "0.0",
			//	"rainWeek": "5.3",
			//	"rainMonth": "6.8",
			//	"rainYear": "572.5",
			//	"rainGain": "1.00",
			//	"rstRainDay": "0",      reset hour - 0=00:00 etc
			//	"rstRainWeek": "1",     0=Sunday 1=Monday
			//	"rstRainYear":"0"       reset month
			//}

			// response = 200 - OK

		}

		public void SetRainTotals(CancellationToken token)
		{
			// http://ip-address/set_rain_totals

			// POST
			//{
			//	"rainDay": "0.0",
			//	"rainWeek": "5.3",
			//	"rainMonth": "6.8",
			//	"rainYear": "572.5",
			//	"rainGain": "1.01",
			//	"rainFallPriority": "1",
			//	"rstRainDay": "0",
			//	"rstRainWeek": "1",
			//	"rstRainYear": "0"
			//}

			// response = 200 - OK

		}


		public void CheckForUpgrade(CancellationToken token)
		{
			// http://ip-address/upgrade_process

			// POST
			// {"upgrade": "check"}

			// response
			//{
			//	"is_new": false,
			//	"msg": "It's the latest version\r\nCurrent version:V2.3.4\r\n- Optimize RF reception performance.\r\n- Fix the issue of incorrect voltage upload for wh34/wh35/wh68 batteries."
			//}

		}

		public void StartUpgrade(CancellationToken token)
		{
			// http://ip-address/upgrade_process

			// POST
			// {"upgrade": "start"}

			// response
			// object
			// status: N   1=running 
			// 'over'

		}

		public void Login(string password, CancellationToken token)
		{
			// http://ip-address/set_login_info

			// POST
			//{pwd: "base64_string"}
		}


		public void Reboot(CancellationToken token)
		{
			// http://ip-address/set_device_info

			// POST
			// { sysreboot: 1 }

		}

		private static string decodePassword(string base64EncodedData)
		{
			var base64EncodedBytes = System.Convert.FromBase64String(base64EncodedData);
			return System.Text.Encoding.UTF8.GetString(base64EncodedBytes);
		}

		private static string encodePassword(string plainText)
		{
			var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
			return System.Convert.ToBase64String(plainTextBytes);
		}


		public void Dispose()
		{
			try
			{
			}
			catch
			{
				// do nothing
			}
		}

		public class CommonSensor
		{
			public string id { get; set; }
			public string val { get; set; }
			public string? unit { get; set; }
			public double? battery { get; set; }

			public int? valInt
			{
				get
				{
					if (val.EndsWith('%'))
						val = val[0..^1];
					return int.TryParse(val, out int result) ? result : null;
				}
			}

			public double? valDbl
			{
				get
				{
					if (val.Contains(' '))
					{
						var temp = val.Split(' ');
						unit = temp[1];
						val = temp[0];
					}
					return double.TryParse(val, invNum, out double result) ? result : null;
				}
			}
		}

		public class TempHumSensor
		{
			public int channel { get; set; }
			public int? battery { get; set; }
			public double? temp { get; set; }
			public string? humidity { get; set; }
			public string? unit { get; set; }

			public int? humidityVal
			{
				get
				{
					return int.TryParse(humidity[0..^1], out int result) ? result : null;
				}
			}
		}


		public class Wh25Sensor
		{
			public double intemp { get; set; }
			public string unit { get; set; }
			public string inhumi { get; set; }
			public string abs { get; set; }
			public string rel { get; set; }
			public int? battery { get; set; }

			public int? inhumiInt
			{
				get
				{
					return int.TryParse(inhumi[0..^1], out int result) ? result : null;
				}
			}
		}

		public class LightningSensor
		{
			public string distance { get; set; }
			public string timestamp { get; set; }
			public int? count { get; set; }
			public int? battery { get; set; }

			public double? distanceVal
			{
				get
				{
					var temp = distance.Split(' ');
					return double.TryParse(temp[0], invNum, out double result) ? result : null;
				}
			}

			public string distanceUnit
			{
				get
				{
					var temp = distance.Split(' ');
					return temp[1];
				}
			}
		}

		public class Co2Sensor
		{
			public double? temp { get; set; }
			public string unit { get; set; }
			public string humidity { get; set; }
			public double? PM25 { get; set; }
			public double? PM25_RealAQI { get; set; }
			public double? PM25_24HAQI { get; set; }
			public double? PM10 { get; set; }
			public double? PM10_RealAQI { get; set; }
			public double? PM10_24HAQI { get; set; }
			public int? CO2 { get; set; }
			public int? CO2_24H { get; set; }
			public int? battery { get; set; }

			public int? humidityVal
			{
				get
				{
					return int.TryParse(humidity[0..^1], out int result) ? result : null;
				}
			}
		}

		public class ChPm25Sensor
		{
			public int? channel { get; set; }
			public double? PM25 { get; set; }
			public double? PM25_RealAQI { get; set; }
			public double? PM25_24HAQI { get; set; }
			public int? battery { get; set; }
		}

		public class ChLeakSensor
		{
			public int? channel { get; set; }
			public string name { get; set; }
			public int? battery { get; set; }
			public string status { get; set; }
		}

		public class LiveData
		{
			public CommonSensor[] common_list { get; set; }
			public CommonSensor[]? rain { get; set; }
			public CommonSensor[]? piezoRain { get; set; }
			public Wh25Sensor[]? wh25 { get; set; }
			public LightningSensor[]? lightning { get; set; }
			public Co2Sensor[]? co2 { get; set; }
			public ChPm25Sensor[]? ch_pm25 { get; set; }
			public ChLeakSensor[]? ch_leak { get; set; }
			public TempHumSensor[]? ch_aisle { get; set; }
			public TempHumSensor[]? ch_soil { get; set; }
			public TempHumSensor[]? ch_temp { get; set; }
			public TempHumSensor[]? ch_leaf { get; set; }
		}

		public class SensorInfo
		{
			public string img {  get; set; }
			public int type { get; set; }
			public string name { get; set; }
			public string id { get; set; }
			public int batt { get; set; }
			public int signal { get; set; }
			public bool idst { get; set; }
		}
	}
}