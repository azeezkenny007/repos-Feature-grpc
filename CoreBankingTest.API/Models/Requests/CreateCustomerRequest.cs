namespace CoreBankingTest.API.Models.Requests
{
    public record CreateCustomerRequest
    {
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string Phone { get; init; } = string.Empty;
        public string Address { get; init; } = string.Empty;
        public string BVN { get; init; } = string.Empty;
        public DateTime DateOfBirth { get; init; }
    }
}
