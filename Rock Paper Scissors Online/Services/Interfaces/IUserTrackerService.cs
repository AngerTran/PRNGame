namespace Rock_Paper_Scissors_Online.Services.Interfaces
{
    public interface IUserTrackerService
    {
        /// <summary>
        /// ghi nhận kết nối của một user
        /// </summary>
        Task AddConnectionAsyns(string userId, string connectionId);

        /// <summary>
        /// ghi nhận ngắt kết nối của một user
        /// </summary>
        Task RemoveConnectionAsync(string connectionId);

        /// <summary>
        /// lấy id của một user đang online
        /// </summary> 
        Task<IEnumerable<string>> GetOnlineUserId();
    }
}
