using System;

namespace CheckInApi.Models;

public record QrLoginRequest(string Username, Guid QrSecret);
public record ForgotPasswordRequest(string Email);
public record ChangePasswordResetRequest(string NewPassword);

public record CheckInRequest(Guid MemberId);
public record CheckOutRequest(Guid LogId);

public record CreateVolunteerDto(string Username, string Email, string Password, bool CanViewData, bool CanAddUsers, bool CanManageVolunteers, bool CanManageEvents);
public record UpdatePasswordDto(string NewPassword);
public record UpdatePermissionsDto(string Email, bool CanViewData, bool CanAddUsers, bool CanManageVolunteers, bool CanManageEvents);

public record CreateEventDto(string Name, DateOnly EventDate, string RepeatType, int RepeatCount);
