﻿using LagoVista.Core.Models;
using LagoVista.Core.Validation;
using LagoVista.IoT.DeviceAdmin.Models;
using Newtonsoft.Json;
using System.Linq;
using LagoVista.Core;
using System.Text.RegularExpressions;
using System;

namespace LagoVista.IoT.Runtime.Core.Models.PEM
{
    /* 
     * We don't use our standard version because we want to user camelcase in script, 
     * can't use Newtonsoft JsonProperty since these are used in script, bottom line
     * just use as-is
     * */
    public class SimpleGeoCode
    {
        public double latitude { get; set; }
        public double longitude { get; set; }
    }

    public class MessageValue
    {
        public MessageValue(ParameterTypes value)
        {
            Type = EntityHeader<ParameterTypes>.Create(value);
        }

        public MessageValue()
        {

        }

        private string _value;
        public string Name { get; set; }
        public string Key { get; set; }
        [JsonIgnore()]
        public bool HasValue { get { return !string.IsNullOrEmpty(Value); } }
        public string Value
        {
            get { return _value; }
            set
            {
                if (String.IsNullOrEmpty(value))
                {
                    _value = null;
                }
                else
                {
                    _value = value;
                }
            }
        }
        public EntityHeader<UnitSet> UnitSet { get; set; }
        public EntityHeader<StateSet> StateSet { get; set; }
        public EntityHeader<ParameterTypes> Type { get; set; }

        public State GetStateValue()
        {
            return StateSet.Value.States.Where(state => state.Key == Value).FirstOrDefault();
        }

        public double GetDouble()
        {
            if(double.TryParse(Value, out double returnValue))
            {
                return returnValue;
            }

            return 0.0;
        }

        public DateTime GetUniversalDateTime()
        {
            return Value.ToDateTime();
        }

        public int GetIntValue()
        {
            if (int.TryParse(Value, out int returnValue))
            {
                return returnValue;
            }

            return 0;
        }

        public bool GetBooleanValue()
        {
            if(bool.TryParse(Value, out bool returnValue))
            {
                return returnValue;
            }

            return false;
        }

        public string GetString()
        {
            return Value.ToString();
        }

        public SimpleGeoCode GetGeoLocation()
        {
            if(String.IsNullOrEmpty(Value))
            {
                return null;
            }

            var regEx = new Regex(@"^(?'lat'-?\d{1,2}\.\d{6}),(?'lon'-?\d{1,3}\.\d{6})$");
            var match = regEx.Match(Value);

            
            if(double.TryParse(match.Groups["lat"].Value, out double lat) &&
               double.TryParse(match.Groups["lon"].Value, out double lon))
            {
                var geo = new SimpleGeoCode()
                {
                    latitude = lat,
                    longitude = lon
                };
                return geo;
            }

            return null;
        }

        public MessageValue Clone(string key = "", string name = "")
        {
            return new MessageValue()
            {
                Value = this.Value,
                Type = this.Type,
                UnitSet = this.UnitSet,
                StateSet = this.StateSet,
                Name = String.IsNullOrEmpty(name) ? this.Name : name,
                Key = String.IsNullOrEmpty(key) ? this.Key : key,
            };
        }

        /// <summary>
        ///Make sure that the message value contains valid data based on required fields and parameter type  
        /// </summary>
        /// <returns></returns>
        public ValidationResult Validate()
        {
            var result = new ValidationResult();
            if (string.IsNullOrEmpty(Key))
            {
                result.Errors.Add(Resources.ErrorCodes.Messaging.KeyNotSpecified.ToErrorMessage());
                return result;
            }

            if (EntityHeader.IsNullOrEmpty(Type))
            {
                result.Errors.Add(Resources.ErrorCodes.Messaging.TypeNotSpecified.ToErrorMessage($"Key={Key}"));
                return result;
            }

            /* Validation of being required happens upstream, this is only holding a result */
            if (string.IsNullOrEmpty(Value))
            {
                return result;
            }

            switch (Type.Value)
            {
                case ParameterTypes.DateTime:
                    if (!Value.SuccessfulJSONDate()) result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidDateTime.ToErrorMessage($"Key={Key},Value={Value}"));
                    break;
                case ParameterTypes.Decimal:
                    if (!decimal.TryParse(Value, out decimal decValue)) result.Errors.Add(Resources.ErrorCodes.Messaging.InvlidDecimal.ToErrorMessage($"Key={Key},Value={Value}"));
                    break;
                case ParameterTypes.GeoLocation:
                    var regEx = new Regex(@"^(?'lat'-?\d{1,2}\.\d{6}),(?'lon'-?\d{1,3}\.\d{6})$");
                    var match = regEx.Match(Value);
                    if (!match.Success)
                    {
                        result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidGeoLocation.ToErrorMessage($"Key={Key},Value={Value}"));
                    }
                    else
                    {
                        if (double.TryParse(match.Groups["lat"].Value, out double lat) && double.TryParse(match.Groups["lon"].Value, out double lon))
                        {
                            if (lat > 90 || lat < -90) result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidLatitude.ToErrorMessage($"Key={Key},Latitude={lat}"));
                            if (lon > 180 || lon < -180) result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidLongitude.ToErrorMessage($"Key={Key},Longitude={lon}"));
                        }
                        else
                        {
                            result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidGeoLocation.ToErrorMessage($"Key={Key},Value={Value}"));
                        }
                    }
                    break;
                case ParameterTypes.Integer:
                    if (!int.TryParse(Value, out int intValue)) result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidInteger.ToErrorMessage($"Key={Key},Value={Value}"));
                    break;
                case ParameterTypes.State:
                    if (EntityHeader.IsNullOrEmpty(StateSet)) result.Errors.Add(Resources.ErrorCodes.Messaging.StateSetSpecifiedButNotProvided.ToErrorMessage($"Key={Key}"));
                    if (result.Successful && !StateSet.Value.States.Where(state => state.Key == Value).Any())
                    {
                        var allowable = string.Empty;
                        foreach (var state in StateSet.Value.States)
                        {
                            allowable += string.IsNullOrEmpty(allowable) ? $"[{state.Key}]" : $",[{state.Key}]";
                        }
                        result.Errors.Add(Resources.ErrorCodes.Messaging.InvalidStateValue.ToErrorMessage($"Key={Key},Value={Value},Allowable={allowable.ToString()}"));
                    }
                    break;
                case ParameterTypes.String: /* No Op, if we have a string it's valid */
                    break;
                case ParameterTypes.TrueFalse:
                    if (!bool.TryParse(Value, out bool boolValue)) result.Errors.Add(Resources.ErrorCodes.Messaging.ValueOnUnitSetNotDouble.ToErrorMessage($"Key={Key},Value={Value}"));
                    break;
                case ParameterTypes.ValueWithUnit:
                    if (EntityHeader.IsNullOrEmpty(UnitSet)) result.Errors.Add(Resources.ErrorCodes.Messaging.UnitTypeSpecifiedButnotProvided.ToErrorMessage($"Key={Key}"));
                    if (!double.TryParse(Value, out double dblValue)) result.Errors.Add(Resources.ErrorCodes.Messaging.ValueOnUnitSetNotDouble.ToErrorMessage($"Key={Key},Value={Value}"));
                    break;
            }

            return result;
        }
    }
}
