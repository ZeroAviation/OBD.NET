﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OBD.NET.Common.Commands;
using OBD.NET.Common.Communication;
using OBD.NET.Common.Enums;
using OBD.NET.Common.Events;
using OBD.NET.Common.Events.EventArgs;
using OBD.NET.Common.Extensions;
using OBD.NET.Common.Logging;
using OBD.NET.Common.OBDData;

namespace OBD.NET.Common.Devices
{
    public class ELM327 : SerialDevice
    {
        #region Properties & Fields

        protected readonly Dictionary<Type, IDataEventManager> DataReceivedEventHandlers = new Dictionary<Type, IDataEventManager>();

        protected static Dictionary<Type, byte> PidCache { get; } = new Dictionary<Type, byte>();
        protected static Dictionary<byte, Type> DataTypeCache { get; } = new Dictionary<byte, Type>();

        protected Mode Mode { get; set; } = Mode.ShowCurrentData; //TODO DarthAffe 26.06.2016: Implement different modes

        #endregion

        #region Events 

        public delegate void DataReceivedEventHandler<T>(object sender, DataReceivedEventArgs<T> args) where T : IOBDData;

        public delegate void RawDataReceivedEventHandler(object sender, RawDataReceivedEventArgs args);
        public event RawDataReceivedEventHandler RawDataReceived;

        #endregion

        #region Constructors

        public ELM327(ISerialConnection connection, IOBDLogger logger = null)
            : base(connection, logger: logger)
        { }

        #endregion

        #region Methods

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            InternalInitialize();
        }

        public override void Initialize()
        {
            base.Initialize();
            InternalInitialize();
        }

        private void InternalInitialize()
        {
            Logger?.WriteLine("Initializing ...", OBDLogLevel.Debug);

            try
            {
                Logger?.WriteLine("Resetting Device ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.ResetDevice);

                Logger?.WriteLine("Turning Echo Off ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.EchoOff);

                Logger?.WriteLine("Turning Linefeeds Off ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.LinefeedsOff);

                Logger?.WriteLine("Turning Headers Off ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.HeadersOff);

                Logger?.WriteLine("Turning Spaced Off ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.PrintSpacesOff);

                Logger?.WriteLine("Setting the Protocol to 'Auto' ...", OBDLogLevel.Debug);
                SendCommand(ATCommand.SetProtocolAuto);

            }
            // DarthAffe 21.02.2017: This seems to happen sometimes, i don't know why - just retry.
            catch
            {
                Logger?.WriteLine("Failed to initialize the device!", OBDLogLevel.Error);
                throw;
            }
        }
        
        /// <summary>
        /// Sends the AT command.
        /// </summary>
        /// <param name="command">The command.</param>
        public virtual void SendCommand(ATCommand command) => SendCommand(command.Command);

        /// <summary>
        /// Requests the data and calls the handler
        /// </summary>
        /// <typeparam name="T"></typeparam>
        public virtual void RequestData<T>()
            where T : class, IOBDData, new()
        {
            Logger?.WriteLine("Requesting Type " + typeof(T).Name + " ...", OBDLogLevel.Debug);

            byte pid = ResolvePid<T>();
            RequestData(pid);
        }

        protected virtual void RequestData(byte pid)
        {
            Logger?.WriteLine("Requesting PID " + pid.ToString("X2") + " ...", OBDLogLevel.Debug);
            SendCommand(((byte)Mode).ToString("X2") + pid.ToString("X2"));
        }

        /// <summary>
        /// Requests the data asynchronous and return the data when available
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public virtual async Task<T> RequestDataAsync<T>()
            where T : class, IOBDData, new()
        {
            Logger?.WriteLine("Requesting Type " + typeof(T).Name + " ...", OBDLogLevel.Debug);
            byte pid = ResolvePid<T>();
            Logger?.WriteLine("Requesting PID " + pid.ToString("X2") + " ...", OBDLogLevel.Debug);
            CommandResult result = SendCommand(((byte)Mode).ToString("X2") + pid.ToString("X2"));

            await result.WaitHandle.WaitAsync();
            return result.Result as T;
        }

        protected override object ProcessMessage(string message)
        {
            DateTime timestamp = DateTime.Now;

            RawDataReceived?.Invoke(this, new RawDataReceivedEventArgs(message, timestamp));

            if (message.Length > 4)
            {
                if (message[0] == '4')
                {
                    byte mode = (byte)message[1].GetHexVal();
                    if (mode == (byte)Mode)
                    {
                        byte pid = (byte)message.Substring(2, 2).GetHexVal();
                        if (DataTypeCache.TryGetValue(pid, out Type dataType))
                        {
                            IOBDData obdData = (IOBDData)Activator.CreateInstance(dataType);
                            obdData.Load(message.Substring(4, message.Length - 4));

                            if (DataReceivedEventHandlers.TryGetValue(dataType, out IDataEventManager dataEventManager))
                                dataEventManager.RaiseEvent(this, obdData, timestamp);

                            return obdData;
                        }
                    }
                }
            }
            return null;
        }

        protected virtual byte ResolvePid<T>()
            where T : class, IOBDData, new()
        {
            if (!PidCache.TryGetValue(typeof(T), out byte pid))
            {
                T data = Activator.CreateInstance<T>();
                pid = data.PID;
                PidCache.Add(typeof(T), pid);
                DataTypeCache.Add(pid, typeof(T));
            }

            return pid;
        }

        public override void Dispose() => Dispose(true);

        public void Dispose(bool sendCloseProtocol)
        {
            try
            {
                if (sendCloseProtocol)
                    SendCommand(ATCommand.CloseProtocol);
            }
            catch { /* Well at least we tried ... */ }

            DataReceivedEventHandlers.Clear();

            base.Dispose();
        }

        public void SubscribeDataReceived<T>(DataReceivedEventHandler<T> eventHandler) where T : IOBDData
        {
            if (!DataReceivedEventHandlers.TryGetValue(typeof(T), out IDataEventManager eventManager))
                DataReceivedEventHandlers.Add(typeof(T), (eventManager = new GenericDataEventManager<T>()));

            ((GenericDataEventManager<T>)eventManager).DataReceived += eventHandler;
        }

        public void UnsubscribeDataReceived<T>(DataReceivedEventHandler<T> eventHandler) where T : IOBDData
        {
            if (DataReceivedEventHandlers.TryGetValue(typeof(T), out IDataEventManager eventManager))
                ((GenericDataEventManager<T>)eventManager).DataReceived -= eventHandler;
        }

        #endregion
    }
}
