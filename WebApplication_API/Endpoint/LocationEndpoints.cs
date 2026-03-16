using System;
using Microsoft.EntityFrameworkCore;
using WebApplication_API.Data;
using WebApplication_API.DTO;
using WebApplication_API.Model;

namespace WebApplication_API.Endpoint;

public static class LocationEndpoints
{
    const string EndpointName = "GetLocation";

    public static void MapLocationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/locations");
        group.MapGet("/", async (DBContext dbcontext) =>
            await dbcontext.Locations.Include(l => l.Category).Select(l =>
                new LocationDTO(l.Id, l.Name, l.Address, l.Category!.Name, l.EstablishedYear, l.Description, l.Latitude, l.Longitude, l.OwnerName, l.WebURL, l.Phone, l.Email, l.NumOfAudio, l.NumOfImg, l.NumOfPeopleVisited, l.Status)).ToListAsync()
        );

        group.MapGet("/{id}", async (int id, DBContext dbcontext) =>
        {
            var location = await dbcontext.Locations.FindAsync(id);
            return location is null ? Results.NotFound() : Results.Ok(
                new LocationDTO(location.Id, location.Name, location.Address, location.Category!.Name, location.EstablishedYear, location.Description, location.Latitude, location.Longitude, location.OwnerName, location.WebURL, location.Phone, location.Email, location.NumOfAudio, location.NumOfImg, location.NumOfPeopleVisited, location.Status)
            );
        }).WithName(EndpointName);

        group.MapPost("/", async (CreateLocationDTO locationDto, DBContext dbcontext) =>
        {
            Location newLocation = new()
            {
                Name = locationDto.Name,
                Address = locationDto.Address,
                CategoryId = locationDto.CategoryId,
                EstablishedYear = locationDto.EstablishedYear,
                Description = locationDto.Description,
                Latitude = locationDto.Latitude,
                Longitude = locationDto.Longitude,
                OwnerName = locationDto.OwnerName,
                WebURL = locationDto.WebURL,
                Phone = locationDto.Phone,
                Email = locationDto.Email,
                Status = locationDto.Status
            };
            dbcontext.Locations.Add(newLocation);
            await dbcontext.SaveChangesAsync();
            LocationDTO locationInfo = new(
                newLocation.Id,
                newLocation.Name,
                newLocation.Address,
                newLocation.Category!.Name,
                newLocation.EstablishedYear,
                newLocation.Description,
                newLocation.Latitude,
                newLocation.Longitude,
                newLocation.OwnerName,
                newLocation.WebURL,
                newLocation.Phone,
                newLocation.Email,
                newLocation.NumOfAudio,
                newLocation.NumOfImg,
                newLocation.NumOfPeopleVisited,
                newLocation.Status
            );
            return Results.CreatedAtRoute(EndpointName, new { id = locationInfo.Id }, locationInfo);
        });


        group.MapPut("/{id}", async (int id, UpdateLocationDTO locationDto, DBContext dbcontext) =>
        {
            var existingLocation = await dbcontext.Locations.FindAsync(id);
            if (existingLocation is null)
            {
                return Results.NotFound();
            }
            existingLocation.Name = locationDto.Name;
            existingLocation.Address = locationDto.Address;
            existingLocation.CategoryId = locationDto.CategoryId;
            existingLocation.EstablishedYear = locationDto.EstablishedYear;
            existingLocation.Description = locationDto.Description;
            existingLocation.Latitude = locationDto.Latitude;
            existingLocation.Longitude = locationDto.Longitude;
            existingLocation.OwnerName = locationDto.OwnerName;
            existingLocation.WebURL = locationDto.WebURL;
            existingLocation.Phone = locationDto.Phone;
            existingLocation.Email = locationDto.Email;
            existingLocation.Status = locationDto.Status;
            await dbcontext.SaveChangesAsync();
            return Results.NoContent();
        });

        group.MapDelete("/{id}", async (int id, DBContext dbcontext) =>
        {
            await dbcontext.Locations.Where(l => l.Id == id).ExecuteDeleteAsync();
            return Results.NoContent();
        });

    }
}
