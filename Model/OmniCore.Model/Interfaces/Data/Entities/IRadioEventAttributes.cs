﻿using OmniCore.Model.Enumerations;

namespace OmniCore.Model.Interfaces.Data.Entities
{
    public interface IRadioEventAttributes
    {
        RadioEvent EventType { get; set; }
        bool Success { get; set; }
        byte[] Data { get; set; }
    }
}
