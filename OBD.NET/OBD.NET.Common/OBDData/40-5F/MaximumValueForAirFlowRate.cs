﻿using OBD.NET.Common.DataTypes;

namespace OBD.NET.Common.OBDData
{
    public class MaximumValueForAirFlowRate : AbstractOBDData
    {
        #region Properties & Fields

        public GramPerSec Value => new GramPerSec(A * 10, 0, 2550);

        #endregion

        #region Constructors

        public MaximumValueForAirFlowRate()
            : base(0x50, 4)
        { }

        #endregion
    }
}
