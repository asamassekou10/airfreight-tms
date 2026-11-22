-- Minimal reference data for the M1 examples. Expand with reviewed catalog releases.
\set ON_ERROR_STOP on
BEGIN;

CREATE TABLE countries (
    code text PRIMARY KEY CHECK (code ~ '^[A-Z]{2}$'),
    name text NOT NULL
);
CREATE TABLE airports (
    code text PRIMARY KEY CHECK (code ~ '^[A-Z]{3}$'),
    country_code text NOT NULL REFERENCES countries,
    name text NOT NULL
);
CREATE TABLE incoterms (
    edition smallint NOT NULL,
    code text NOT NULL CHECK (code ~ '^[A-Z]{3}$'),
    name text NOT NULL,
    PRIMARY KEY (edition, code)
);

INSERT INTO countries (code, name) VALUES ('US', 'United States'), ('DE', 'Germany');
INSERT INTO airports (code, country_code, name) VALUES
    ('ORD', 'US', 'Chicago O’Hare International Airport'),
    ('DFW', 'US', 'Dallas Fort Worth International Airport'),
    ('FRA', 'DE', 'Frankfurt Airport');
INSERT INTO incoterms (edition, code, name) VALUES
    (2020, 'FCA', 'Free Carrier'),
    (2020, 'CPT', 'Carriage Paid To'),
    (2020, 'DAP', 'Delivered at Place');

GRANT SELECT ON countries, airports, incoterms TO :"runtime_role";
COMMIT;
