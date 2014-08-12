using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace NServiceBus.Core.Tests.Deserializing
{
    using System.Linq;
    using MessageInterfaces.MessageMapper.Reflection;
    using NServiceBus.Pipeline.Contexts;
    using Serialization;
    using Unicast;
    using Unicast.Messages;

    public class ExtractLogicalMessageBehaviorTests
    {
        [Test]
        public void MultipleInheritanceWithUnknownRootClass()
        {
            var mapper = new MessageMapper();
            mapper.Initialize(new[] { typeof(IMyFirstEvent), typeof(IMySecondEvent) });

            var metadataRegistry = new MessageMetadataRegistry();
            metadataRegistry.RegisterMessageType(typeof(IMyFirstEvent));
            metadataRegistry.RegisterMessageType(typeof(IMySecondEvent));

            var behavior = new ExtractLogicalMessagesBehavior
            {
                UnicastBus = new UnicastBus(),
                MessageSerializer = new NopSerializer(mapper),
                LogicalMessageFactory = new LogicalMessageFactory
                {
                    MessageMapper = mapper,
                    MessageMetadataRegistry = metadataRegistry
                },
                MessageMetadataRegistry = metadataRegistry
            };

            var transportMessage = new TransportMessage();
            transportMessage.Headers.Add(Headers.EnclosedMessageTypes, string.Join(";", typeof(MyEvent), typeof(IMyFirstEvent), typeof(IMySecondEvent)));
            transportMessage.Body = new byte[1];

            var context = new ReceivePhysicalMessageContext(null, transportMessage, false);

            behavior.Invoke(context, () => { });

            Assert.IsInstanceOf<IMyFirstEvent>(context.LogicalMessages.Single().Instance);
            Assert.IsInstanceOf<IMySecondEvent>(context.LogicalMessages.Single().Instance);
        }

        public interface IMyFirstEvent : IMessage // Using message to avoid checks for sending events
        {
        }

        public interface IMySecondEvent : IMessage
        {
        }

        public class MyEvent : IMyFirstEvent, IMySecondEvent
        {
        }

        public class NopSerializer : IMessageSerializer
        {
            private readonly MessageMapper mapper;

            public NopSerializer(MessageMapper mapper)
            {
                this.mapper = mapper;
            }

            public void Serialize(object[] messages, Stream stream)
            {
                throw new NotImplementedException();
            }

            public object[] Deserialize(Stream stream, IList<Type> messageTypes = null)
            {
                return new[]
                {
                    mapper.CreateInstance(messageTypes.First())
                };
                //if (messageTypes == null)
                //    return new object[0];

                //var result = new object[messageTypes.Count];

                //var i = 0;
                //foreach (var messageType in messageTypes)
                //{
                //    result[i++] = mapper.CreateInstance(messageType);
                //}

                //return result;
            }

            public string ContentType
            {
                get { throw new NotImplementedException(); }
            }
        }
    }
}