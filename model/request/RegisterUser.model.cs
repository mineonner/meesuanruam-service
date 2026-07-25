namespace meesuanruam_service.model.request
{
    public class RegisterUser
    {
        public string user_email { get; set; }
        public string? password { get; set; }
        public string? user_otp { get; set; }
    }
}
