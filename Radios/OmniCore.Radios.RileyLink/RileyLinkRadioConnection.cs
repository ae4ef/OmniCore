﻿using OmniCore.Model.Exceptions;
using OmniCore.Model.Interfaces;
using OmniCore.Repository;
using OmniCore.Repository.Entities;
using OmniCore.Repository.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace OmniCore.Radios.RileyLink
{
    public class RileyLinkRadioConnection : IRadioConnection
    {
        public IRadioPeripheralLease PeripheralLease { get;  }
        private IRadioPeripheral Peripheral { get => PeripheralLease.Peripheral; }
        private IDisposable ConnectedSubscription = null;
        private IDisposable ConnectionFailedSubscription = null;
        private IDisposable DisconnectedSubscription = null;
        private IDisposable DeviceChangedSubscription = null;
        private IDisposable DeviceLostSubscription = null;
        private IDisposable ResponseNotifySubscription = null;

        private Radio RadioEntity;
        private PodRequest Request;

        private Guid RileyLinkServiceUUID = Guid.Parse("0235733b-99c5-4197-b856-69219c2a3845");
        private Guid RileyLinkDataCharacteristicUUID = Guid.Parse("c842e849-5028-42e2-867c-016adada9155");
        private Guid RileyLinkResponseCharacteristicUUID = Guid.Parse("6e6c7910-b89e-43a5-a0fe-50c5e2b81f4a");

        private IRadioPeripheralCharacteristic DataCharacteristic;
        private IRadioPeripheralCharacteristic ResponseCharacteristic;

        private ConcurrentQueue<(byte? ResponseNo, byte[] Response)> Responses;
        private AsyncManualResetEvent ResponseEvent;

        private RileyLinkRadioConfiguration ActiveConfiguration = null;

        public RileyLinkRadioConnection(IRadioPeripheralLease radioPeripheralLease, Radio radioEntity, PodRequest request)
        {
            Responses = new ConcurrentQueue<(byte?,byte[])>();
            ResponseEvent = new AsyncManualResetEvent();
            RadioEntity = radioEntity;
            Request = request;
            PeripheralLease = radioPeripheralLease;
            SubscribeToDeviceStates();
            SubscribeToConnectionStates();
        }

        public async Task<bool> Initialize(IRadioConfiguration radioConfiguration, CancellationToken cancellationToken)
        {
            var configuration = radioConfiguration as RileyLinkRadioConfiguration;
            if (configuration == null)
                return false;

            if (!Peripheral.IsConnected)
            {
                ResponseCharacteristic = null;
                DataCharacteristic = null;
                if (await Peripheral.Connect(true, cancellationToken))
                {
                    var characteristics = await Peripheral.GetCharacteristics(RileyLinkServiceUUID,
                        new[] { RileyLinkResponseCharacteristicUUID, RileyLinkDataCharacteristicUUID }, cancellationToken);
                    ResponseCharacteristic = characteristics[0];
                    DataCharacteristic = characteristics[1];

                    if (ResponseCharacteristic == null || DataCharacteristic == null)
                        throw new OmniCoreRadioException(FailureType.RadioUnknownError, "GATT characteristics not found");

                    ResponseNotifySubscription = ResponseCharacteristic.WhenNotificationReceived().Subscribe(async (_) =>
                    {
                        var counterData = await ResponseCharacteristic.Read(TimeSpan.FromMilliseconds(2000), CancellationToken.None);
                        var counter = counterData?[0];
                        var commandResponse = await DataCharacteristic.Read(TimeSpan.FromMilliseconds(2000), CancellationToken.None);
                        Responses.Enqueue((counter, commandResponse));
                        ResponseEvent.Set();
                    });
                }
            }

            if (ActiveConfiguration != null)
            {
                if (await VerifyConfiguration(configuration, cancellationToken))
                {

                }
            }
            else
            {
                if (await ConfigureRileyLink(configuration, cancellationToken))
                {
                    ActiveConfiguration = configuration;
                }
            }
            return ActiveConfiguration != null;
        }

        public async Task<IMessage> ExchangeMessages(IMessage messageToSend, CancellationToken cancellationToken, TxPower? TxLevel = null)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            ResponseNotifySubscription?.Dispose();
            ConnectedSubscription?.Dispose();
            ConnectionFailedSubscription?.Dispose();
            DisconnectedSubscription?.Dispose();
            DeviceChangedSubscription?.Dispose();
            DeviceLostSubscription?.Dispose();
            PeripheralLease?.Dispose();
        }

        private void SubscribeToDeviceStates()
        {
            Peripheral.WhenDeviceChanged().Subscribe(async (_) =>
            {
                ConnectedSubscription?.Dispose();
                ConnectionFailedSubscription?.Dispose();
                DisconnectedSubscription?.Dispose();
                SubscribeToConnectionStates();
                //TODO: reset gatt related mumbojumbo
            });

            Peripheral.WhenDeviceLost().Subscribe(async (_) =>
            {
                //TODO: request peripheral device replacement
            });
        }

        private void SubscribeToConnectionStates()
        {
            ConnectedSubscription = Peripheral.WhenConnected().Subscribe( async (_) =>
            {
                var rssi = await Peripheral.ReadRssi();
                using (var rcr = RepositoryProvider.Instance.RadioConnectionRepository)
                {
                    await rcr.Create(new RadioConnection
                    {
                        RadioId = RadioEntity.Id.Value,
                        PodId = Request?.PodId,
                        RequestId = Request?.Id,
                        EventType = RadioConnectionEvent.Connect,
                        Successful = true
                    });
                }

                using (var ssr = RepositoryProvider.Instance.SignalStrengthRepository)
                {
                    await ssr.Create(new SignalStrength { RadioId = RadioEntity.Id.Value,
                        ClientRadioRssi = rssi });
                }
            });

            ConnectionFailedSubscription = Peripheral.WhenConnectionFailed().Subscribe( async (err) =>
            {
                using (var rcr = RepositoryProvider.Instance.RadioConnectionRepository)
                {
                    await rcr.Create(new RadioConnection
                    {
                        RadioId = RadioEntity.Id.Value,
                        PodId = Request?.PodId,
                        RequestId = Request?.Id,
                        EventType = RadioConnectionEvent.Connect,
                        Successful = false,
                        ErrorText = err.Message
                    });
                }
            });

            DisconnectedSubscription = Peripheral.WhenDisconnected().Subscribe( async (_) =>
            {
                using (var rcr = RepositoryProvider.Instance.RadioConnectionRepository)
                {
                    await rcr.Create(new RadioConnection
                    {
                        RadioId = RadioEntity.Id.Value,
                        PodId = Request?.PodId,
                        RequestId = Request?.Id,
                        EventType = RadioConnectionEvent.Disconnect,
                        Successful = true
                    });
                }
            });
        }

        private async Task<bool> ConfigureRileyLink(RileyLinkRadioConfiguration radioConfiguration, CancellationToken cancellationToken)
        {
            await SendCommand(cancellationToken, RileyLinkCommandType.ResetRadioConfig);
            await SendCommand(cancellationToken, RileyLinkCommandType.SetSwEncoding, new byte[] { (byte)RileyLinkSoftwareEncoding.None });
            await SendCommand(cancellationToken, RileyLinkCommandType.SetPreamble, new byte[] { 0x55, 0x55 });

            var registers = radioConfiguration.GetConfiguration();
            foreach (var register in registers)
                await SendCommand(cancellationToken, RileyLinkCommandType.UpdateRegister, new[] { (byte)register.Item1, (byte)register.Item2 });

            var result = await SendCommand(cancellationToken, RileyLinkCommandType.GetState);
            if (result.Length != 2 || result[0] != 'O' || result[1] != 'K')
                throw new OmniCoreRadioException(FailureType.RadioStateError, "RL returned status not OK.");

            return true;
        }

        private async Task<bool> VerifyConfiguration(RileyLinkRadioConfiguration radioConfiguration, CancellationToken cancellationToken)
        {
            return true;
        }

        private async Task<byte[]> SendCommand(CancellationToken cancellationToken, RileyLinkCommandType cmd, byte[] cmdData = null)
        {
            try
            {
                byte[] data;
                if (cmdData == null)
                {
                    data = new byte[] { 1, (byte)cmd };
                }
                else
                {
                    data = new byte[cmdData.Length + 2];
                    data[0] = (byte)(cmdData.Length + 1);
                    data[1] = (byte)cmd;
                    Buffer.BlockCopy(cmdData, 0, data, 2, cmdData.Length);
                }

                var result = await SendCommandAndGetResponse(data, cancellationToken);

                if (result == null || result.Length == 0)
                    throw new OmniCoreRadioException(FailureType.RadioDisconnectPrematurely, "RL returned no result");

                else if (result[0] == (byte)RileyLinkResponseType.OK
                    || result[0] == (byte)RileyLinkResponseType.Interrupted)
                {
                    if (result.Length > 1)
                    {
                        var response = new byte[result.Length - 1];
                        Buffer.BlockCopy(result, 1, response, 0, response.Length);
                        return response;
                    }
                    else
                        return null;
                }
                else if (result[0] == (byte)RileyLinkResponseType.Timeout)
                    throw new OmniCoreTimeoutException(FailureType.RadioRecvTimeout);
                else
                    throw new OmniCoreRadioException(FailureType.RadioUnknownError, $"RL returned error code {result[0]}");
            }
            catch (OmniCoreException) { throw; }
            catch (Exception e)
            {
                throw new OmniCoreRadioException(FailureType.RadioDisconnectPrematurely, "Error while sending a command via BLE", e);
            }
        }

        private async Task<byte[]> SendCommandAndGetResponse(byte[] dataToWrite, CancellationToken cancellationToken)
        {
            ResponseEvent.Reset();
            await DataCharacteristic.Write(dataToWrite, TimeSpan.FromMilliseconds(2000), cancellationToken);
            await ResponseEvent.WaitAsync(cancellationToken);
            (byte?, byte[]) result;
            while(Responses.TryDequeue(out result))
            {
            }
            return result.Item2;
        }
    }
}