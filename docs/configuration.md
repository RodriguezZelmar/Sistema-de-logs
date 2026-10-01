# Configuration

> No configuration options exist yet. This document will list every option
> (name, type, default value, validation rules) as it is implemented.

Options planned for the first version:

- minimum log level
- log file path
- maximum file size before rotation
- maximum number of retained files
- output format
- enabling/disabling logging

Configuring from code is the primary goal. Loading options from an external
`appsettings.json` file will be evaluated once the code-based configuration
is complete.
