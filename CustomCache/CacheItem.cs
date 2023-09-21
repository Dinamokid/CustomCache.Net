using System.Runtime.CompilerServices;
namespace CustomCache;

public interface ICacheItem<out T>
    {
        public DateTime CreatedDateTime { get; }
        public T Value { get; }

        /// <summary>
        /// Вычислить absolute expiration.
        /// </summary>
        TimeSpan GetAbsoluteExpiration();

        /// <summary>
        /// Проверить softExpiration интервал
        /// </summary>
        bool IsExpired();

        bool IsNotExpired();
    }

    public sealed class CacheItem<T> : ICacheItem<T>
    {
        private const int DefaultExpirationDelta = 20; // percent

        public DateTime CreatedDateTime { get; }
        public T Value { get; }
        private readonly TimeSpan _softExpiration;

        private DateTime SoftExpirationDate
            => CreatedDateTime + _softExpiration;

        public CacheItem(T value, TimeSpan expiration)
        {
            Value = value;
            CreatedDateTime = DateTime.UtcNow;
            _softExpiration = GetSoftExpiration(expiration);
        }

        public bool IsExpired()
            => DateTime.UtcNow >= SoftExpirationDate;

        public bool IsNotExpired()
            => !IsExpired();

        public TimeSpan GetAbsoluteExpiration()
        {
            const double targetAbsoluteExpirationCoeff = 1.2;
            var minSoftExpirationCoeff = GetSoftExpirationCoefficient(DefaultExpirationDelta);
            var absoluteExpirationCoeff = targetAbsoluteExpirationCoeff / minSoftExpirationCoeff;
            // absolute expiration должно быть больше чем дата создания плюс soft expiration

            // Если softExpiration короткий, то данные в кэше быстро протухнут. Чтобы фоновое обновление кэша работало,
            // нужно запрос попадал на промежуток, когда softExpiration уже прошел, а absoluteExpiration еще нет.
            return absoluteExpirationCoeff * _softExpiration;
        }

        /// <summary>
        /// Возвращает <see cref="TimeSpan"/> с временной меткой, случайно размазанной относительно <paramref name="expiration"/>
        /// soft expiration должно быть меньше чем absolute expiration
        /// </summary>
        private static TimeSpan GetSoftExpiration(TimeSpan expiration)
        {
#pragma warning disable SCS0005
            var delta = Random.Shared.Next(0, DefaultExpirationDelta);
#pragma warning restore SCS0005
            return expiration * GetSoftExpirationCoefficient(delta);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double GetSoftExpirationCoefficient(double delta)
            => 1 - delta / 100;
    }