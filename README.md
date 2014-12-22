DigiFramework
=============

DigiFramework is a .NET (C#) state machine and actor framework for robots or machine automation.  The 
framework itself has no hardware dependencies though, and may be useful for situations outside of hardware
automation.

The framework is made up of the following conceptual components:
* Actors - Isolated process (conceptually), which interacts with other actors only via messages
* Hierarchical Finite State Machine - Manages the state of a given system, and provides for events and/or conditions for state transitions.
* Stateful Actors - Combines actors with state machines
* Logging, error handling, and other utilitary features

The implementation is made up of the following components:
* ProxyBuilder - Inspired by [NAct](https://code.google.com/p/n-act/), dynamically creates a class which implements a given interface, and routs all interface interaction to a single invoke handler.  This is used to implement actors using interface invocations rather than explicit messages, but is also potentially useful to create a variety of aspect oriented capabilities, such as cross cutting error handling or logging.
* StateMachine - Inspired by [Stateless](https://code.google.com/p/stateless/), this hierarchical state machine is strongly typed and hierarchical.  Also provides facilities for conditional, automatic state transitions, which can be useful for implementing "when all" scenarios.
* WorkerQueue - serializes execution onto a single conceptual thread.  Actual threads are created as-needed.  This is used by ActorBase to implement actors, but is also useful for a variety of scenarios such as creating a worker thread which sits along a user interface, avoiding the situation where the user might accidentally queue up more than one UI action asynchronously.
* ActorBase - implements actor functionality using the ProxyBuilder and the WorkerQueue.  Also self-manages exceptions and provides call tracing.
* StatefulActor - combines actor with state machine.  Automatically handles simple state commands and provides for command verification.
* LinqExtensions - helpful IEnumerable extensions, such as Remove, Expand, DistinctyBy, and Each
* Logging - logging stub including filtering.  Easily adapted to any logging framework.
 
Example
-------

    // This is the actor interface - what the actor "looks like" from outside
    public interface IMotorController : IStatefulActor<MotorControllerStates, MotorControllerTriggers>, IMotorControllerMethods
    {
      void Start();
      void Stop();
    }
    
    // This is the interface that the actor class actually implements.  The Start/Stop commands are handled by configuration.
    public interface IMotorControllerMethods
    {
      /// <remarks> Left and right = 0 means active braking. </remarks>
      /// <param name="left"> 1 = full forward, -1 = full reverse. </param>
      /// <param name="right"> 1 = full forward, -1 = full reverse. </param>
      void SetSpeed(float left, float right);
      
      /// <summary> Disables the motor controller to save power.  If moving, the motors will coast. </summary>
      void Standby();
    }
    
    public enum MotorControllerStates
    {
      Unstarted,
      Starting,
      Started,
      Ready,
      Commanding,
      Stopping
    }
    
    public enum MotorControllerTriggers
    {
      Start,
      Started,
      Completed,
      Command,
      Stop,
      Errored,
      Faulted,
      Stopped,
    }
    
    // This is configuration for the actor; unrelated to this framework
    public class MotorControllerConfiguration
    {
      	public BbbPort Motor0Direction0Gpio { get; set; }
      	public BbbPort Motor0Direction1Gpio { get; set; }
      	public BbbPort Motor0SpeedPwm { get; set; }
      
      	public BbbPort Motor1Direction0Gpio { get; set; }
      	public BbbPort Motor1Direction1Gpio { get; set; }
      	public BbbPort Motor1SpeedPwm { get; set; }
      
      	public BbbPort StandbyGpio { get; set; }
      
      	public int Period { get; set; }
      
      	public static readonly MotorControllerConfiguration Default = 
      		new MotorControllerConfiguration
      		{
      			Motor0Direction0Gpio = BbbPort.P8_12,
      			Motor0Direction1Gpio = BbbPort.P8_11,
      			Motor0SpeedPwm = BbbPort.P9_14,
      			Motor1Direction0Gpio = BbbPort.P8_16,
      			Motor1Direction1Gpio = BbbPort.P8_15,
      			Motor1SpeedPwm = BbbPort.P8_13,
      			StandbyGpio = BbbPort.P8_17,
      			Period = 14285	// 75kHz
      		};
    }
    
    	// This is the actor implementation.  Note that it only implements IMotorControllerMethods
    public class MotorController : StatefulActor<IMotorController, MotorControllerStates, MotorControllerTriggers>, IMotorControllerMethods
    {
    	public MotorControllerConfiguration Configuration { get; private set; }
    	
    	public MotorController(MotorControllerConfiguration configuration)
    	{
    		Configuration = configuration;
    	}
    
    	protected override StateMachine<MotorControllerStates, MotorControllerTriggers> InitializeStates()
    	{
    		return NewStateMachine
    		(
    			new [] 
    			{
    				NewState
    				(
    					MotorControllerStates.Unstarted, 
    					null, 
    					new [] { NewTransition(MotorControllerTriggers.Start, MotorControllerStates.Starting) }
    				),
    				NewState
    				(
    					MotorControllerStates.Starting, 
    					null, 
    					new [] 
    					{ 
    						NewTransition(MotorControllerTriggers.Started, MotorControllerStates.Started),
    						NewTransition(MotorControllerTriggers.Errored, MotorControllerStates.Unstarted),
    						NewTransition(MotorControllerTriggers.Faulted, MotorControllerStates.Unstarted)
    					},
    					StartingEntered
    				),
    				NewState
    				(
    					MotorControllerStates.Started, 
    					null, 
    					new [] 
    					{ 
    						NewTransition(MotorControllerTriggers.Stop, MotorControllerStates.Stopping),
    						NewTransition(MotorControllerTriggers.Faulted, MotorControllerStates.Stopping)
    					}
    				),
    				NewState
    				(
    					MotorControllerStates.Stopping, 
    					null, 
    					new [] 
    					{ 
    						NewTransition(MotorControllerTriggers.Stopped, MotorControllerStates.Unstarted),
    						NewTransition(MotorControllerTriggers.Errored, MotorControllerStates.Unstarted),
    						NewTransition(MotorControllerTriggers.Faulted, MotorControllerStates.Unstarted)
    					},
    					StoppingEntered
    				),
    			}
    		);
    	}
    
    	// This sets up the valid commands
    	protected override IReadOnlyDictionary<string, Command<MotorControllerStates, MotorControllerTriggers>> InitializeCommands()
    	{
    		return new Dictionary<string, Command<MotorControllerStates, MotorControllerTriggers>>
    			{
    				{ "Start", NewCommand(new MotorControllerStates[] { MotorControllerStates.Unstarted }, MotorControllerTriggers.Start) },
    				{ "Stop", NewCommand(new MotorControllerStates[] { MotorControllerStates.Started }, MotorControllerTriggers.Stop) },
    				{ "SetSpeed", NewCommand(new MotorControllerStates[] { MotorControllerStates.Started }) },
    				{ "Standby", NewCommand(new MotorControllerStates[] { MotorControllerStates.Started }) },
    			};
    	}
    
    	protected override void HandleError(Exception e)
    	{
    		Fire(MotorControllerTriggers.Errored);
    	}
    
    	protected override void HandleFault(Exception e)
    	{
    		Fire(MotorControllerTriggers.Faulted);
    	}
    
    	private Pwm[] speeds;
    	private Gpio[] forwards;
    	private Gpio[] backwards;
    	private Gpio standby;
    
    	private void StartingEntered(MotorControllerStates oldState, StateMachine<MotorControllerStates, MotorControllerTriggers>.Transition transition)
    	{
    		speeds = new Pwm[]
    			{
    				new Pwm(Configuration.Motor0SpeedPwm, false),
    				new Pwm(Configuration.Motor1SpeedPwm, false)
    			};
    		speeds.Each(s => 
    			{ 
    				s.Configure();
    				s.Run = false; 
    				// HACK: The BBB driver has a problem setting period when more than one PWM port is active.
    				try
    				{
    					s.Period = Configuration.Period;
    				}
    				catch
    				{
    					// Swallow error.  
    				}
    				s.DutyPercent = 0; 
    				s.Run = true; 
    			}
    		);
    
    		forwards =
    			new Gpio[]
    			{
    				new Gpio(Configuration.Motor0Direction0Gpio),
    				new Gpio(Configuration.Motor1Direction0Gpio)
    			};
    		forwards.Each(s => { s.Direction = BbbDirection.Out; s.Value = 0; });
    
    		backwards =
    			new Gpio[]
    			{
    				new Gpio(Configuration.Motor0Direction1Gpio),
    				new Gpio(Configuration.Motor1Direction1Gpio)
    			};
    		backwards.Each(s => { s.Direction = BbbDirection.Out; s.Value = 0; });
    
    		standby = new Gpio(Configuration.StandbyGpio);
    		standby.Direction = BbbDirection.Out;
    		standby.Value = 1;
    
    		Fire(MotorControllerTriggers.Started);
    	}
    
    	public void SetSpeed(float left, float right)
    	{
    		SetMotor(0, left);
    		SetMotor(1, right);
    		standby.Value = 1;
    	}
    
    	private void SetMotor(int motorNumber, float speed)
    	{
    		forwards[motorNumber].Value = speed >= 0 ? 1 : 0;
    		backwards[motorNumber].Value = speed <= 0 ? 1 : 0;
    		speeds[motorNumber].DutyPercent = Math.Max(0f, Math.Min(100f, Math.Abs(speed * 100)));
    	}
    
    	public void Standby()
    	{
    		standby.Value = 0;
    	}
    
    	private void StoppingEntered(MotorControllerStates oldState, StateMachine<MotorControllerStates, MotorControllerTriggers>.Transition transition)
    	{
    		standby.Value = 0;
    		standby = null;
    		backwards.Each(s => { s.Value = 0; });
    		backwards = null;
    		forwards.Each(s => { s.Value = 0; });
    		forwards = null;
    		speeds.Each(s => { s.Run = false; });
    		speeds = null;
    		// TODO: consider option to unconfigure ports
    
    		Fire(MotorControllerTriggers.Stopped);
    	}
    }
	
I hope to add more examples, including how to use the state machine's Condition to automatically advance states.
