namespace Stint.Cli.Components
{
    /// <summary>
    /// The four-digit HH:mm mask used wherever a time is typed in (Home's custom clock-in/out,
    /// the time rows on Settings): digits are appended one at a time into <c>--:--</c>, and the
    /// result only counts once all four are in and form a real time.
    /// </summary>
    public sealed class TimeDigitsInput
    {
        #region Fields

        private string _digits = string.Empty;

        #endregion

        #region Properties

        /// <summary>The mask with whatever's typed so far, e.g. "08:3-".</summary>
        public string Display
        {
            get
            {
                var padded = _digits.PadRight(4, '-');
                return $"{padded[0]}{padded[1]}:{padded[2]}{padded[3]}";
            }
        }

        /// <summary>
        /// Index into <see cref="Display"/> of the next digit to be typed - the renderer's cue
        /// for where to draw the "cursor" - or null once all four digits are in. Skips the colon.
        /// </summary>
        public int? CursorIndex => _digits.Length switch
        {
            0 => 0,
            1 => 1,
            2 => 3,
            3 => 4,
            _ => null
        };

        /// <summary>Whether all four digits are in and form a valid time of day.</summary>
        public bool IsComplete => TryParse(out _);

        #endregion

        #region Methods

        /// <summary>Appends one digit - ignored if it isn't an ASCII digit or the mask is already full.</summary>
        public void Append(char digit)
        {
            if (char.IsAsciiDigit(digit) && _digits.Length < 4)
            {
                _digits += digit;
            }
        }

        /// <summary>Removes the last digit. Returns false if there was nothing to remove.</summary>
        public bool Backspace()
        {
            if (_digits.Length == 0)
            {
                return false;
            }

            _digits = _digits[..^1];
            return true;
        }

        /// <summary>Clears every digit typed so far.</summary>
        public void Clear() => _digits = string.Empty;

        /// <summary>Parses the typed digits - false unless all four are in and the hour/minute are in range.</summary>
        public bool TryParse(out TimeOnly time)
        {
            time = default;
            if (_digits.Length != 4)
            {
                return false;
            }

            var hour = int.Parse(_digits[..2]);
            var minute = int.Parse(_digits[2..]);
            if (hour is < 0 or > 23 || minute is < 0 or > 59)
            {
                return false;
            }

            time = new TimeOnly(hour, minute);
            return true;
        }

        #endregion
    }
}
