namespace Employeemanagementpractice.Services
{
    public interface ISaIdValidationService
    {
        (bool IsValid, string? ErrorMessage, DateTime? DateOfBirth, string? Gender) ValidateSAID(string idNumber);
    }

    public class SaIdValidationService : ISaIdValidationService
    {
        /// <summary>
        /// Validates a South African ID number (13 digits).
        /// Format: YYMMDD GSSS CAZ
        /// YY = Year, MM = Month, DD = Day
        /// G = Gender (0-4 Female, 5-9 Male)
        /// SSS = Sequence, C = Citizenship (0=SA, 1=PR)
        /// A = Usually 8, Z = Luhn check digit
        /// </summary>
        public (bool IsValid, string? ErrorMessage, DateTime? DateOfBirth, string? Gender) ValidateSAID(string idNumber)
        {
            if (string.IsNullOrWhiteSpace(idNumber))
                return (false, "ID number is required", null, null);

            idNumber = idNumber.Trim();

            if (idNumber.Length != 13)
                return (false, "SA ID number must be exactly 13 digits", null, null);

            if (!idNumber.All(char.IsDigit))
                return (false, "SA ID number must contain only digits", null, null);

            // Extract date of birth
            int year = int.Parse(idNumber.Substring(0, 2));
            int month = int.Parse(idNumber.Substring(2, 2));
            int day = int.Parse(idNumber.Substring(4, 2));

            // Determine century
            year += year >= 0 && year <= 26 ? 2000 : 1900;

            if (month < 1 || month > 12)
                return (false, "Invalid month in ID number", null, null);

            if (day < 1 || day > 31)
                return (false, "Invalid day in ID number", null, null);

            DateTime dateOfBirth;
            try
            {
                dateOfBirth = new DateTime(year, month, day);
            }
            catch
            {
                return (false, "Invalid date of birth in ID number", null, null);
            }

            if (dateOfBirth > DateTime.Today)
                return (false, "Date of birth cannot be in the future", null, null);

            // Gender
            int genderDigit = int.Parse(idNumber.Substring(6, 1));
            string gender = genderDigit >= 5 ? "Male" : "Female";

            // Citizenship digit
            int citizenship = int.Parse(idNumber.Substring(10, 1));
            if (citizenship != 0 && citizenship != 1)
                return (false, "Invalid citizenship digit in ID number", null, null);

            // Luhn algorithm check
            if (!ValidateLuhn(idNumber))
                return (false, "ID number failed checksum validation", null, null);

            return (true, null, dateOfBirth, gender);
        }

        private bool ValidateLuhn(string number)
        {
            int sum = 0;
            bool alternate = false;

            for (int i = number.Length - 1; i >= 0; i--)
            {
                int n = int.Parse(number[i].ToString());

                if (alternate)
                {
                    n *= 2;
                    if (n > 9)
                        n -= 9;
                }

                sum += n;
                alternate = !alternate;
            }

            return sum % 10 == 0;
        }
    }
}
