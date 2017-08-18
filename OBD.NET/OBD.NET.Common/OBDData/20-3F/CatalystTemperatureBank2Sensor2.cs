﻿using OBD.NET.Common.DataTypes;

namespace OBD.NET.Common.OBDData
{
    public class CatalystTemperatureBank2Sensor2 : AbstractOBDData
    {
        #region Properties & Fields

        public DegreeCelsius Temperature => new DegreeCelsius((((256 * A) + B) / 10.0) - 40, -40, 6513.5);

        #endregion

        #region Constructors

        public CatalystTemperatureBank2Sensor2()
            : base(0x3F, 2)
        { }

        #endregion
    }
}
