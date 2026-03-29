using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Newtonsoft.Json;

namespace PocketBaseSdk
{
    public delegate void SubscriptionFunc(SseMessage e);

    public delegate Task UnsubscribeFunc();

    public class RealtimeService : BaseService
    {
        private SseClient _sse;
        private readonly Dictionary<string, SubscriptionFunc> _subscriptions = new();

        public string ClientId { get; private set; }

        /// <summary>
        /// An optional hook that is invoked when the realtime client disconnects
        /// either when unsubscribing from all subscriptions or when the
        /// connection was interrupted or closed by the server.
        /// </summary>
        /// <remarks>
        /// <para>
        /// It receives the subscriptions' map before the disconnect
        /// (could be used to determine whether the disconnect was caused by
        /// unsubscribing or network/server error).
        /// </para>
        /// <para>
        /// If you want to listen to the opposite, aka.
        /// When the client connection is established, subscribe to the `PB_CONNECT` event.
        /// </para>
        /// </remarks>
        public Action<Dictionary<string, SubscriptionFunc>> OnDisconnect;

        public RealtimeService(PocketBase client) : base(client)
        {
        }

        /// <summary>
        /// Register the subscription listener.
        /// </summary>
        /// <remarks>
        /// You can subscribe multiple times to the same topic.
        ///
        /// If the SSE connection is not started yet,
        /// this method will also initialize it.
        /// </remarks>
        /// <example>
        /// Here is an example listening to the connect/reconnect events:
        /// <code lang="csharp">
        /// pb.Realtime.Subscribe("PB_CONNECT", (e) {
        ///   Debug.Log("Connected: ${e}");
        /// });
        /// </code>
        /// </example>
        public async Task<UnsubscribeFunc> Subscribe(
            string topic,
            SubscriptionFunc listener,
            string expand = null,
            string filter = null,
            string fields = null,
            Dictionary<string, object> query = null,
            Dictionary<string, string> headers = null)
        {
            var key = topic;

            // Merge query parameters
            Dictionary<string, object> enrichedQuery = new(query ?? new());

            if (!string.IsNullOrEmpty(expand))
                enrichedQuery.TryAdd("expand", expand);

            if (!string.IsNullOrEmpty(filter))
                enrichedQuery.TryAdd("filter", filter);

            if (!string.IsNullOrEmpty(fields))
                enrichedQuery.TryAdd("fields", fields);

            // Serialize and append the topic options (if any)
            var options = new Dictionary<string, object>();

            if (enrichedQuery.Count > 0)
            {
                options["query"] = enrichedQuery;
            }

            if (headers?.Count > 0)
            {
                options["headers"] = headers;
            }

            if (options.Count > 0)
            {
                var encoded = $"options={HttpUtility.UrlEncode(JsonConvert.SerializeObject(options))}";
                key += (key.Contains("?") ? "&" : "?") + encoded;
            }

            if (!_subscriptions.TryAdd(key, listener))
            {
                _subscriptions[key] += listener;
            }

            if (_sse == null)
            {
                await Connect();
            }
            else if (!string.IsNullOrEmpty(ClientId) && _subscriptions[key] != null)
            {
                await SubmitSubscriptions();
            }

            return async () => { await UnsubscribeByTopicAndListener(topic, listener); };
        }

        /// Unsubscribe from all subscription listeners with the specified topic.
        ///
        /// If <paramref name="topic"/> is not set, then this method will unsubscribe
        /// from all active subscriptions.
        ///
        /// This method is no-op if there are no active subscriptions.
        ///
        /// The related sse connection will be autoclosed if after the
        /// unsubscribe operation there are no active subscriptions left.
        public Task Unsubscribe(string topic = null)
        {
            var needToSubmit = false;

            if (string.IsNullOrEmpty(topic))
            {
                _subscriptions.Clear();
            }
            else
            {
                var subs = GetSubscriptionsByTopic(topic);

                foreach (string key in subs.Keys)
                {
                    _subscriptions.Remove(key);
                    needToSubmit = true;
                }
            }

            if (!HasNonEmptyTopic())
            {
                Disconnect();
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(ClientId) && needToSubmit)
            {
                return SubmitSubscriptions();
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Unsubscribe from all subscription listeners starting with
        /// the specified topic prefix.
        /// </summary>
        /// <remarks>
        /// This method is no-op if there are no active subscriptions
        /// with the specified topic prefix.
        ///
        /// The related sse connection will be autoclosed if after the
        /// unsubscribe operation there are no active subscriptions left.
        /// </remarks>
        public Task UnsubscribeByPrefix(string topicPrefix)
        {
            var beforeLength = _subscriptions.Count;

            // Remove matching subscriptions
            // "?" so that it can be used as an end delimiter for the prefix
            _subscriptions.RemoveWhere(kvp => $"{kvp.Key}?".StartsWith(topicPrefix));

            // No changes
            if (beforeLength == _subscriptions.Count)
            {
                return Task.CompletedTask;
            }

            // No more subscriptions, close the sse connection
            if (!HasNonEmptyTopic())
            {
                Disconnect();
                return Task.CompletedTask;
            }

            // Otherwise - notify the server about the subscription changes
            return !string.IsNullOrEmpty(ClientId) ? SubmitSubscriptions() : Task.CompletedTask;
        }

        /// <summary>
        /// Unsubscribe from all subscriptions matching the specified topic
        /// and listener function.
        /// </summary>
        /// <remarks>
        /// This method is no-op if there are no active subscription with
        /// the specified topic and listener.
        ///
        /// The related sse connection will be autoclosed if after the
        /// unsubscribe operation there are no active subscriptions left.
        /// </remarks>
        private Task UnsubscribeByTopicAndListener(
            string topic,
            SubscriptionFunc listener)
        {
            bool needToSubmit = false;

            var subs = GetSubscriptionsByTopic(topic);

            foreach (string key in subs.Keys)
            {
                var subscription = _subscriptions[key];
                if (subscription == null)
                {
                    // Nothing to unsubscribe from.
                    continue;
                }

                var beforeLength = subscription.GetInvocationList().Length;

                _subscriptions[key] -= listener;

                // Re-get the subscription after modification to ensure we have the current state
                var updatedSubscription = _subscriptions[key];
                var afterLength = updatedSubscription?.GetInvocationList().Length ?? 0;

                if (beforeLength == afterLength)
                {
                    // No changes, no need to submit.
                    continue;
                }

                if (!needToSubmit && afterLength == 0)
                {
                    needToSubmit = true;
                }
            }

            if (!HasNonEmptyTopic())
            {
                Disconnect();
                return Task.CompletedTask;
            }

            if (!string.IsNullOrEmpty(ClientId) && needToSubmit)
            {
                return SubmitSubscriptions();
            }

            return Task.CompletedTask;
        }

        private bool HasNonEmptyTopic()
        {
            return _subscriptions.Any(kvp => kvp.Value != null);
        }

        private Task Connect()
        {
            Disconnect();

            var completer = new TaskCompletionSource<bool>();
            string url = _client.BuildUrl("/api/realtime");

            _sse = new SseClient(url);
            _sse.OnClose += () =>
            {
                if (!string.IsNullOrEmpty(ClientId))
                {
                    OnDisconnect?.Invoke(_subscriptions);
                }

                Disconnect();

                if (!completer.Task.IsCompleted)
                {
                    completer.TrySetException(new Exception("failed to establish SSE connection"));
                }
            };
            _sse.OnError += _ =>
            {
                if (!string.IsNullOrEmpty(ClientId))
                {
                    ClientId = string.Empty;
                    OnDisconnect?.Invoke(_subscriptions);
                }
            };

            // Bind subscription listener
            _sse.OnMessage += msg =>
            {
                if (!_subscriptions.TryGetValue(msg.Event, out var subscription))
                {
                    return;
                }

                subscription?.Invoke(msg);
            };

            // Resubmit local subscriptions on first reconnect
            _sse.OnMessage += async msg =>
            {
                if (msg.Event != "PB_CONNECT")
                {
                    return;
                }

                ClientId = msg.Id;
                await SubmitSubscriptions();

                if (!completer.Task.IsCompleted)
                {
                    completer.TrySetResult(true);
                }
            };

            _sse.Connect();

            return completer.Task;
        }

        private void Disconnect()
        {
            _sse?.Close();
            _sse = null;
            ClientId = string.Empty;
        }

        private Task SubmitSubscriptions()
        {
            return _client.Send(
                path: "/api/realtime",
                method: "POST",
                body: new
                {
                    clientId = ClientId,
                    subscriptions = _subscriptions.Keys.ToList()
                }
            );
        }

        private Dictionary<string, SubscriptionFunc> GetSubscriptionsByTopic(string topic)
        {
            var result = new Dictionary<string, SubscriptionFunc>();

            // "?" so that it can be used as an end delimiter for the topic
            topic = topic.Contains("?") ? topic : $"{topic}?";

            foreach (var (key, value) in _subscriptions)
            {
                if ($"{key}?".StartsWith(topic))
                {
                    result[key] = value;
                }
            }

            return result;
        }
    }
}
