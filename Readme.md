**SKY BOOKER 🛫**



SkyBooker is a full-stack Airline Ticket Booking System built using .NET 8, ASP.NET Core Web API, and Angular. The platform allows users to search flights, view available options, select seats, enter passenger details, and complete bookings with generated PNRs through a clean and modular interface.



The system supports Guests (who can browse and search flights), Passengers (who can register, log in, and book tickets), and Airline Staff (who manage flights, seat availability, and passenger records). It is designed using a microservices architecture with separate services for authentication, flights, seats, bookings, and passengers, all connected through an API Gateway.



Key features include seat selection, smart seat suggestions based on availability and position, secure authentication using JWT, and optimized performance using Redis caching. The application is containerized using Docker and demonstrates scalable, modular backend design suitable for real-world systems.



🧾 UC1: User Registration \& Login (Auth Service)

🎯 Objective



Allow users to create an account and securely log in to access the system.



👤 Actors

Guest (not logged in)

Passenger

Airline Staff

✅ Features

🔹 Register Account



Users can create an account by providing:



Full Name

Email

Password

Role (Passenger / Staff)



✔ Each email must be unique

✔ Password is securely stored



🔹 Login



Registered users can log in using:



Email

Password



✔ On successful login, a secure token (JWT) is generated

✔ This token is used for accessing protected features later

