-- The postgres image creates the 'auth' database (POSTGRES_DB=auth).
-- Create the 'game' database as well (EF Core migrations create the tables inside it):
CREATE DATABASE game;
