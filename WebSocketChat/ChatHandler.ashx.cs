using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.WebSockets;

namespace WebSocketChat
{
    /// <summary>
    /// Сводное описание для ChatHandler
    /// </summary>
    public class ChatHandler : IHttpHandler
    {

        /// <summary>
        /// Список всех клиентов
        /// </summary>
        private static readonly List<WebSocket> Clients = new List<WebSocket>();

        /// <summary>
        /// Блокировка для обеспечения потокабезопасности
        /// </summary>
        private static readonly ReaderWriterLockSlim Locker = new ReaderWriterLockSlim();

        public void ProcessRequest(HttpContext context)
        {
            // Если запрос является запросом веб сокета
            if (context.IsWebSocketRequest)
                context.AcceptWebSocketRequest(WebSocketRequest);
        }

        private async Task WebSocketRequest(AspNetWebSocketContext context)
        {
            // Получаем сокет клиента из контекста запроса
            var socket = context.WebSocket;

            // Добавляем сокет клиента в список клиентов
            Locker.EnterWriteLock();
            try
            {
                Clients.Add(socket);
            }
            finally
            {
                Locker.ExitWriteLock();
            }

            // Слушаем клиента
            while (true)
            {
                var buffer = new ArraySegment<byte>(new byte[1024]);

                // Ожидаем данные от клиента
                var result = await socket.ReceiveAsync(buffer, CancellationToken.None);

                // Передаем сообщение всем клиентам
                for(int i = 0; i < Clients.Count; i++)
                {
                    WebSocket client = Clients[i];

                    try
                    {
                        if (client.State == WebSocketState.Open)
                        {
                            await client.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                        }
                    }

                    catch (ObjectDisposedException)
                    {
                        Locker.EnterWriteLock();
                        try
                        {
                            Clients.Remove(client);
                            i--;
                        }
                        finally
                        {
                            Locker.ExitWriteLock();
                        }
                    }
                }
            }
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }

}