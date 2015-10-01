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

This example uses [BlackNet](https://github.com/Digithought/) to interface with a dual motor controller.

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
      Stopping
    }
    
    public enum MotorControllerTriggers
    {
      Start,
      Started,
      Completed,
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

The above is used something like this:

	var controller = new MotorController(config).Actor;
	controller.Start();
	controller.StateChanged += (o, t) => { if (t.Target == MotorControllerStates.Started) controller.SetSpeed(0.5f, -0.5f); };

The implementation above looks pretty straight forward hopefully.  Here are some subtleties:
* Note that the MotorController implementation can be written in a single-threaded manner.  This is a big advantage to actors: no shared state, just write your actor single threadedly.
* No logic is needed to check that the controller is in the correct state before setting the speed or what not; this is accomplished by the declaration of which commands are valid during which states in InitializeCommands.
* The Start and Stop commands aren't implemented in the class, yet they are available in the actor interface.  Magic!
* Errors are handled by the actor, not propagated to the callers.  If an observer needs to now if an actor faults, for instance, use WatchOther if in another actor, or  hooking the StateChanged event; all calls are fire-and-forget by default.  Any functions that cause an exception will result in the default value for the return type.
* If a trigger is fired during a state transition (as often they are due to logic in "...Entered" events) the trigger doesn't fire until the transition completes.  This ensures that state events never appear to be out of order.

Here is an example snippet which shows how to implement WatchOtherAndUpdate, WatchOtherWhileInState, and conditions:

		...
		NewState
		(
			MainsStates.GoingOnline, 
			MainsStates.Initialized, 
			new [] 
			{ 
				NewTransition(MainsTriggers.Online, MainsStates.Online, OnlineWhen),
			},
			GoingOnlineEntered
		),
		NewState
		(
			MainsStates.Online, 
			MainsStates.Initialized, 
			null,
			OnlineEntered
		),
		...

	private void GoingOnlineEntered(MainsStates oldState, StateMachine<MainsStates, MainsTriggers>.Transition transition)
	{
		Collection.Begin();
				WatchOtherAndUpdate(Collection);

		FrameUploader.Start();
				WatchOtherAndUpdate(FrameUploader);
	}

	private bool OnlineWhen(MainsStates oldState, StateMachine<MainsStates, MainsTriggers>.Transition transition)
	{
		return Collection.InState(CollectionStates.Operating) && FrameUploader.InState(UploaderStates.Started);
	}

	private void OnlineEntered(MainsStates oldState, StateMachine<MainsStates, MainsTriggers>.Transition transition)
	{
		WatchOtherWhileInState(Collection, (s, t) => !Collection.InState(CollectionStates.Operating), () => { throw new FrameworkException("Collection stopped operating."); }, MainsStates.Online);
		WatchOtherWhileInState(FrameUploader, (s, t) => !FrameUploader.InState(UploaderStates.Started), () => { throw new FrameworkException("FrameUploader went offline."); }, MainsStates.Online);
	}

This example shows part of an actor which is responsible for managing other actors.  It attempts to startup a couple other actors and watches for their states to change.  Any time their states change, UpdateStates() will by called by WatchOtherAndUpdate(), which will re-check the OnlineWhen condition.  Once that condition goes true, the Online trigger automatically fires per the configuration above.  Once online, WatchOtherWhileInState is used to monitor that the other actors remain in the expected states.  Note that InState is used rather than equals, because there are several sub-states of Operating and Started, and all this actor cares is that those actors remain in the said super-states.  Note that an exception is thrown rather than simply firing a trigger, such as Errored.  Typically the HandleError of any stateful actor is hooked to fire such an error trigger, but this way, the reason for the trigger firing will be apparent in the log.  

Another thing to note is that the optional last argument is passed to WatchOtherWhileInState.  All "Watch" actor methods have such an optional argument to  specifies what state *this* actor must be in to continue monitoring the other actor.  This is because when the state transitions through a super-state, into a sub-state, the new state will be the sub-state so the watch will only be kept while in the sub-state.  When using Watch... methods within the entered transition of a super-state, you will almost always want to pass the super-state to the "whileIn" parameter so the watch persists for the duration of the super-state.  

