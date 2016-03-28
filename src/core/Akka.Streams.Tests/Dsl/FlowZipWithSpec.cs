﻿using System;
using System.Linq;
using System.Reactive.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.TestKit;
using Akka.Streams.TestKit.Tests;
using FluentAssertions;
using Xunit;

namespace Akka.Streams.Tests.Dsl
{
    public class FlowZipWithSpec : BaseTwoStreamsSetup<int>
    {
        protected override TestSubscriber.Probe<int> Setup(IPublisher<int> p1, IPublisher<int> p2)
        {
            var subscriber = TestSubscriber.CreateProbe<int>(this);
            Source.FromPublisher<int, Unit>(p1)
                .ZipWith(Source.FromPublisher<int, Unit>(p2), (i, i1) => i + i1)
                .RunWith(Sink.FromSubscriber<int, Unit>(subscriber), Materializer);
            return subscriber;
        }

        [Fact]
        public void A_ZipWith_for_Flow_must_work_in_the_happy_case()
        {
            var probe = TestSubscriber.CreateManualProbe<int>(this);
            Source.From(Enumerable.Range(1, 4))
                .ZipWith(Source.From(new[] {10, 20, 30, 40}.AsEnumerable()), (i, i1) => i + i1)
                .RunWith(Sink.FromSubscriber<int, Unit>(probe), Materializer);

            var subscription = probe.ExpectSubscription();

            subscription.Request(2);
            probe.ExpectNext(11);
            probe.ExpectNext(22);

            subscription.Request(1);
            probe.ExpectNext(33);
            subscription.Request(1);
            probe.ExpectNext(44);

            probe.ExpectComplete();
        }

        [Fact]
        public void A_ZipWith_for_Flow_must_work_in_the_sad_case()
        {
            var probe = TestSubscriber.CreateManualProbe<int>(this);
            Source.From(Enumerable.Range(1, 4))
                .ZipWith(Source.From(Enumerable.Range(-2, 4)), (i, i1) => i/i1)
                .RunWith(Sink.FromSubscriber<int, Unit>(probe), Materializer);

            var subscription = probe.ExpectSubscription();

            subscription.Request(2);
            probe.ExpectNext(1/-2);
            probe.ExpectNext(2/-1);

            EventFilter.Exception<DivideByZeroException>().ExpectOne(() => subscription.Request(2));
            var error = probe.ExpectError();
            error.Should().BeOfType<DivideByZeroException>();
            //can't check for words because the message is localized
            error.Message.Should().Contain("0 (null)");

            probe.ExpectNoMsg(TimeSpan.FromMilliseconds(200));
        }

        [Fact]
        public void A_Zip_for_Flow_must_work_with_one_immediately_completed_and_one_nonempty_publisher()
        {
            var subscriber1 = Setup(CompletedPublisher<int>(), NonEmptyPublisher(Enumerable.Range(1, 4)));
            subscriber1.ExpectSubscriptionAndComplete();

            var subscriber2 = Setup(NonEmptyPublisher(Enumerable.Range(1, 4)), CompletedPublisher<int>());
            subscriber2.ExpectSubscriptionAndComplete();
        }

        [Fact]
        public void A_Zip_for_Flow_must_work_with_one_delayed_completed_and_one_nonempty_publisher()
        {
            var subscriber1 = Setup(SoonToCompletePublisher<int>(), NonEmptyPublisher(Enumerable.Range(1, 4)));
            subscriber1.ExpectSubscriptionAndComplete();

            var subscriber2 = Setup(NonEmptyPublisher(Enumerable.Range(1, 4)), SoonToCompletePublisher<int>());
            subscriber2.ExpectSubscriptionAndComplete();
        }

        [Fact]
        public void A_Zip_for_Flow_must_work_with_one_immediately_failed_and_one_nonempty_publisher()
        {
            var subscriber1 = Setup(FailedPublisher<int>(), NonEmptyPublisher(Enumerable.Range(1, 4)));
            subscriber1.ExpectSubscriptionAndError().Should().BeOfType<ApplicationException>();

            var subscriber2 = Setup(NonEmptyPublisher(Enumerable.Range(1, 4)), FailedPublisher<int>());
            subscriber2.ExpectSubscriptionAndError().Should().BeOfType<ApplicationException>();
        }

        [Fact]
        public void A_Zip_for_Flow_must_work_with_one_delayed_failed_and_one_nonempty_publisher()
        {
            var subscriber1 = Setup(SoonToFailPublisher<int>(), NonEmptyPublisher(Enumerable.Range(1, 4)));
            subscriber1.ExpectSubscriptionAndError().Should().BeOfType<ApplicationException>();

            var subscriber2 = Setup(NonEmptyPublisher(Enumerable.Range(1, 4)), SoonToFailPublisher<int>());
            subscriber2.ExpectSubscriptionAndError().Should().BeOfType<ApplicationException>();
        }
    }
}