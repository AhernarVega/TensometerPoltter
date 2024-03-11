using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Sockets;
using System.Net;
using System.Windows;

namespace TensometerPoltter.Models
{
    internal class UDPModel
    {
        // Класс для передачи данных по UDP
        UdpClient udpClient;
        // Порт для прослушки
        readonly int remotePort;
        // Порт для отправки
        readonly int localPort;

        // Отправить сообщение по UDP
        // Если не указывать _remotePort, будет использован тот, что был указан при инициализации объекта UDPModel
        public async Task<bool> SendMessageAsync(string message, string _receiverIP, int _remotePort = 0)
        {
            // Проверка на корректность IP
            if (!IPAddress.TryParse(_receiverIP, out _))
            {
                MessageBox.Show("Неверно указан IP адрес!", "Error IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            // Преобразование в массив байт
            byte[] data = Encoding.UTF8.GetBytes(message);
            // Определение IP адреса получаетля
            IPEndPoint receiverIP = new(IPAddress.Parse(_receiverIP), (_remotePort == 0) ? remotePort : _remotePort);
            // Проверка отправки
            var bytes = await udpClient.SendAsync(data, receiverIP);
            return (bytes > 0);
        }

        // Получить сооющение по UDP
        // Если не указывать _localPort, будет использован тот, что был указан при инициализации объекта UDPModel
        public async Task<List<byte>> ReceiveMessageAsync(int _localPort = 0)
        {
            // Если есть пакеты для чтения
            if (udpClient.Available > 0)
            {
                // Получение результата
                var result = await udpClient.ReceiveAsync();
                // Получаю массив byte[]
                var message = result.Buffer;
                // Список, куда сохраняются значения
                List<byte> temp = message.ToList();

                return temp;
            }
            return new List<byte>();
        }

        // Очистка буфера UDP от данных
        public void ClearBuffer()
        {
            IPEndPoint? endPoint = null;
            while (udpClient.Available > 0)
            {
                _ = udpClient.Receive(ref endPoint);
            }
        }

        public UDPModel(int _localPort, int _remotePort = 0)
        {
            remotePort = _remotePort;
            localPort = _localPort;
            udpClient = new UdpClient(localPort);
        }
    }
}
